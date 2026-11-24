using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Polling;
using GitOpsRelay.Core.State;

namespace GitOpsRelay.Core.Tests;

public sealed class RefSnapshotStoreContractTests
{
    [Fact]
    public async Task InterruptedBaselineSeedRemainsIncompleteAndRetryResumes()
    {
        var store = new TestRefSnapshotStore();
        var repositoryId = CanonicalRepositoryId.FromRemote("https://github.com/Octo/App.git");

        await Assert.ThrowsAsync<IOException>(() => store.TrySeedBaselineAsync(
            repositoryId,
            InterruptedSnapshot(),
            CancellationToken.None).AsTask());

        Assert.False(await store.IsBaselineSeededAsync(repositoryId));
        var completed = await store.TrySeedBaselineAsync(repositoryId, CompleteSnapshot(), CancellationToken.None);

        Assert.True(completed);
        Assert.True(await store.IsBaselineSeededAsync(repositoryId));
        Assert.Equal(2, await CountAsync(store.ListActiveRefsAsync(repositoryId)));
    }

    [Fact]
    public async Task ActiveRefEnumerationExcludesDeletedTombstones()
    {
        var store = new TestRefSnapshotStore();
        var repositoryId = CanonicalRepositoryId.FromRemote("https://github.com/Octo/App.git");
        await store.TryCompareAndSwapRefAsync(repositoryId, "refs/heads/main", null, TestData.Sha1);
        var created = await store.ReadRefAsync(repositoryId, "refs/heads/main", CancellationToken.None);
        await store.TryCompareAndSwapRefAsync(repositoryId, "refs/heads/main", created!.Version, null);

        Assert.Empty(await CollectAsync(store.ListActiveRefsAsync(repositoryId)));
    }

    private static async IAsyncEnumerable<RefSnapshotSeed> InterruptedSnapshot()
    {
        yield return new RefSnapshotSeed("refs/heads/main", TestData.Sha1);
        await Task.Yield();
        throw new IOException("Stream interrupted");
    }

    private static async IAsyncEnumerable<RefSnapshotSeed> CompleteSnapshot()
    {
        yield return new RefSnapshotSeed("refs/heads/main", TestData.Sha1);
        yield return new RefSnapshotSeed("refs/tags/v1", TestData.Sha1Next);
        await Task.CompletedTask;
    }

    private static async Task<int> CountAsync(IAsyncEnumerable<RefSnapshot> refs) =>
        (await CollectAsync(refs)).Count;

    private static async Task<List<RefSnapshot>> CollectAsync(IAsyncEnumerable<RefSnapshot> refs)
    {
        var snapshots = new List<RefSnapshot>();
        await foreach (var snapshot in refs)
        {
            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private sealed class TestRefSnapshotStore : IRefSnapshotStore
    {
        private readonly Dictionary<(string Repository, string Ref), RefSnapshot> _snapshots = [];
        private readonly HashSet<string> _completedBaselines = [];
        private int _version;

        public async IAsyncEnumerable<RefSnapshot> ListActiveRefsAsync(
            CanonicalRepositoryId repositoryId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var snapshot in _snapshots.Values.Where(
                         snapshot => snapshot.CanonicalRepositoryId == repositoryId.Value && snapshot.IsActive))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return snapshot;
                await Task.Yield();
            }
        }

        public ValueTask<RefSnapshot?> ReadRefAsync(
            CanonicalRepositoryId repositoryId,
            string refName,
            CancellationToken cancellationToken = default)
        {
            _snapshots.TryGetValue((repositoryId.Value, refName), out var snapshot);
            return ValueTask.FromResult(snapshot);
        }

        public ValueTask<RefSnapshotSaveResult> TryCompareAndSwapRefAsync(
            CanonicalRepositoryId repositoryId,
            string refName,
            string? expectedVersion,
            string? objectId,
            CancellationToken cancellationToken = default)
        {
            var key = (repositoryId.Value, refName);
            _snapshots.TryGetValue(key, out var current);
            if (current?.Version != expectedVersion)
            {
                return ValueTask.FromResult(new RefSnapshotSaveResult(
                    current is null ? RefSnapshotSaveStatus.ConflictDeleted : RefSnapshotSaveStatus.ConflictModified,
                    current));
            }

            var saved = new RefSnapshotSaveResult(
                RefSnapshotSaveStatus.Saved,
                new RefSnapshot(repositoryId.Value, refName, objectId, Interlocked.Increment(ref _version).ToString()));
            _snapshots[key] = saved.Snapshot!;
            return ValueTask.FromResult(saved);
        }

        public ValueTask<bool> IsBaselineSeededAsync(
            CanonicalRepositoryId repositoryId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_completedBaselines.Contains(repositoryId.Value));

        public async ValueTask<bool> TrySeedBaselineAsync(
            CanonicalRepositoryId repositoryId,
            IAsyncEnumerable<RefSnapshotSeed> refs,
            CancellationToken cancellationToken = default)
        {
            if (_completedBaselines.Contains(repositoryId.Value))
            {
                return true;
            }

            await foreach (var item in refs.WithCancellation(cancellationToken))
            {
                var key = (repositoryId.Value, item.RefName);
                if (!_snapshots.ContainsKey(key))
                {
                    _snapshots[key] = new RefSnapshot(
                        repositoryId.Value,
                        item.RefName,
                        item.ObjectId,
                        Interlocked.Increment(ref _version).ToString());
                }
            }

            _completedBaselines.Add(repositoryId.Value);
            return true;
        }
    }
}
