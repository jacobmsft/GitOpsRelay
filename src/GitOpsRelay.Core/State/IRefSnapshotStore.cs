using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Polling;

namespace GitOpsRelay.Core.State;

/// <summary>
/// Stores one versioned entity per ref, not a repository-sized map. A null
/// ObjectId is a tombstone; keep tombstones long enough to make CAS ABA-safe.
/// Baseline seeding is silent, streamed, and inserts only missing refs so large
/// repositories do not create a single oversized Azure Table entity.
/// </summary>
public interface IRefSnapshotStore
{
    /// <summary>
    /// Streams active refs for reconstructing the previous polling snapshot.
    /// Tombstones are excluded. Implementations must not materialize the full
    /// repository ref set in a single storage entity.
    /// </summary>
    IAsyncEnumerable<RefSnapshot> ListActiveRefsAsync(
        CanonicalRepositoryId repositoryId,
        CancellationToken cancellationToken = default);

    ValueTask<RefSnapshot?> ReadRefAsync(
        CanonicalRepositoryId repositoryId,
        string refName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CASes one ref. Null expectedVersion means the ref must not exist; a null
    /// objectId writes a tombstone. Success returns the new version token.
    /// </summary>
    ValueTask<RefSnapshotSaveResult> TryCompareAndSwapRefAsync(
        CanonicalRepositoryId repositoryId,
        string refName,
        string? expectedVersion,
        string? objectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently seeds a streamed ref snapshot without emitting signals. Per-ref
    /// writes are resumable: an interrupted enumeration leaves the completion marker
    /// unset, and a retry skips already-seeded refs. Implementations set the completion
    /// marker only after consuming the entire stream and must make the final marker
    /// transition atomic. Returns true when the baseline is complete (already or now).
    /// </summary>
    ValueTask<bool> IsBaselineSeededAsync(
        CanonicalRepositoryId repositoryId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> TrySeedBaselineAsync(
        CanonicalRepositoryId repositoryId,
        IAsyncEnumerable<RefSnapshotSeed> refs,
        CancellationToken cancellationToken = default);
}
