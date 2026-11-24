using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Normalization;

namespace GitOpsRelay.Core.Polling;

public sealed class RefSnapshotDiffEngine(GitSignalNormalizer normalizer)
{
    public async ValueTask<IReadOnlyList<GitSignal>> DiffAsync(
        RepositoryReference repository,
        IngestionInfo ingestion,
        IReadOnlyDictionary<string, string>? previous,
        IReadOnlyDictionary<string, string> observed,
        BaselineSeedMode baselineSeedMode,
        Func<string, string, CancellationToken, ValueTask<bool?>>? isAncestorAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(ingestion);
        ArgumentNullException.ThrowIfNull(observed);

        var oldRefs = previous is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : NormalizeSnapshot(previous);
        var newRefs = NormalizeSnapshot(observed);
        if (previous is null && baselineSeedMode == BaselineSeedMode.Silent)
        {
            return [];
        }

        var signals = new List<GitSignal>();
        foreach (var refName in oldRefs.Keys.Union(newRefs.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            oldRefs.TryGetValue(refName, out var beforeSha);
            newRefs.TryGetValue(refName, out var afterSha);
            if (string.Equals(beforeSha, afterSha, StringComparison.Ordinal))
            {
                continue;
            }

            var refType = refName.StartsWith("refs/heads/", StringComparison.Ordinal) ? "branch" : "tag";
            bool? isForcePush = null;
            if (beforeSha is not null && afterSha is not null && isAncestorAsync is not null)
            {
                var isAncestor = await isAncestorAsync(beforeSha, afterSha, cancellationToken).ConfigureAwait(false);
                isForcePush = isAncestor.HasValue ? !isAncestor.Value : null;
            }

            signals.AddRange(normalizer.CreateRefTransitionSignals(
                repository,
                ingestion,
                refName,
                refType,
                beforeSha,
                afterSha,
                isForcePush));
        }

        return signals;
    }

    public static IReadOnlyDictionary<string, string> NormalizeSnapshot(
        IReadOnlyDictionary<string, string> refs)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (refName, objectId) in refs)
        {
            if (refName.EndsWith("^{}", StringComparison.Ordinal)
                || !refName.StartsWith("refs/heads/", StringComparison.Ordinal)
                && !refName.StartsWith("refs/tags/", StringComparison.Ordinal))
            {
                continue;
            }

            var normalizedObjectId = GitSignalNormalizer.NormalizeObjectId(objectId);
            if (normalizedObjectId is not null)
            {
                normalized[refName] = normalizedObjectId;
            }
        }

        return normalized;
    }
}
