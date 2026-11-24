using GitOpsRelay.Contracts;

namespace GitOpsRelay.Core.Normalization;

public sealed class GitSignalNormalizer
{
    public IReadOnlyList<GitSignal> CreateRefTransitionSignals(
        RepositoryReference repository,
        IngestionInfo ingestion,
        string refName,
        string refType,
        string? beforeSha,
        string? afterSha,
        bool? isForcePush = null,
        IReadOnlyList<string>? changedPaths = null,
        string? actor = null,
        string source = "urn:gitopsrelay")
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(ingestion);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ValidateIngestion(ingestion);

        var normalizedType = NormalizeRefType(refName, refType);
        beforeSha = NormalizeObjectId(beforeSha);
        afterSha = NormalizeObjectId(afterSha);
        if (beforeSha is null && afterSha is null
            || string.Equals(beforeSha, afterSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A ref transition must change the object ID.", nameof(afterSha));
        }

        var normalizedRepository = NormalizeRepository(repository);
        if (beforeSha is null || afterSha is null)
        {
            isForcePush = null;
        }

        var change = new RefChangeInfo
        {
            Ref = refName,
            RefType = normalizedType,
            BeforeSha = beforeSha,
            AfterSha = afterSha,
            IsForcePush = isForcePush,
            ChangedPaths = changedPaths,
            Actor = actor
        };
        var types = beforeSha is null
            ? new[] { normalizedType == "branch" ? GitSignalTypes.BranchCreated : GitSignalTypes.TagCreated, GitSignalTypes.Push }
            : afterSha is null
                ? new[] { normalizedType == "branch" ? GitSignalTypes.BranchDeleted : GitSignalTypes.TagDeleted }
                : new[] { GitSignalTypes.Push };

        return types.Select(type => CreateSignal(
            ingestion,
            type,
            refName,
            source,
            new GitSignalData { Repository = normalizedRepository, Ingestion = ingestion, Change = change },
            IdempotencyKey.Create(normalizedRepository.CanonicalId, type, refName, beforeSha, afterSha))).ToArray();
    }

    public GitSignal CreatePullRequestSignal(
        RepositoryReference repository,
        IngestionInfo ingestion,
        PullRequestInfo pullRequest,
        string source = "urn:gitopsrelay",
        string? eventType = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(ingestion);
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ValidateIngestion(ingestion);
        if (pullRequest.Number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequest), "Pull request number must be positive.");
        }

        var normalizedRepository = NormalizeRepository(repository);
        var headSha = NormalizeObjectId(pullRequest.HeadSha)
            ?? throw new ArgumentException("Pull request head SHA cannot be an all-zero object ID.", nameof(pullRequest));
        var mergeSha = NormalizeObjectId(pullRequest.MergeCommitSha);
        var normalizedPr = pullRequest with { HeadSha = headSha, MergeCommitSha = mergeSha };
        var type = eventType ?? normalizedPr.Status switch
        {
            "open" => GitSignalTypes.PullRequestOpened,
            "merged" => GitSignalTypes.PullRequestMerged,
            "closed" => GitSignalTypes.PullRequestClosed,
            _ => throw new ArgumentException("Pull request status must be open, closed, or merged.", nameof(pullRequest))
        };
        if (type is not (GitSignalTypes.PullRequestOpened or GitSignalTypes.PullRequestUpdated
            or GitSignalTypes.PullRequestMerged or GitSignalTypes.PullRequestClosed))
        {
            throw new ArgumentException("Invalid pull request signal type.", nameof(eventType));
        }

        var expectedStatus = type switch
        {
            GitSignalTypes.PullRequestOpened or GitSignalTypes.PullRequestUpdated => "open",
            GitSignalTypes.PullRequestMerged => "merged",
            GitSignalTypes.PullRequestClosed => "closed",
            _ => throw new ArgumentException("Invalid pull request signal type.", nameof(eventType))
        };
        if (!string.Equals(normalizedPr.Status, expectedStatus, StringComparison.Ordinal))
        {
            throw new ArgumentException("Pull request status does not match the signal event type.", nameof(pullRequest));
        }

        normalizedPr = normalizedPr with
        {
            SourceRef = NormalizePullRequestRef(normalizedPr.SourceRef, nameof(pullRequest)),
            TargetRef = NormalizePullRequestRef(normalizedPr.TargetRef, nameof(pullRequest))
        };
        var key = IdempotencyKey.Create(
            normalizedRepository.CanonicalId,
            type,
            normalizedPr.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            normalizedPr.Status,
            normalizedPr.IsDraft.ToString(),
            normalizedPr.Title,
            normalizedPr.SourceRef,
            normalizedPr.TargetRef,
            normalizedPr.HeadSha,
            normalizedPr.MergeCommitSha);

        return CreateSignal(
            ingestion,
            type,
            $"pr/{normalizedPr.Number}",
            source,
            new GitSignalData { Repository = normalizedRepository, Ingestion = ingestion, PullRequest = normalizedPr },
            key,
            normalizedPr.TargetRef);
    }

    public static string? NormalizeObjectId(string? objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        if (objectId.Length is not (40 or 64) || objectId.Any(static c => !Uri.IsHexDigit(c)))
        {
            throw new ArgumentException("Git object IDs must be 40- or 64-character hexadecimal strings.", nameof(objectId));
        }

        return objectId.All(static c => c == '0') ? null : objectId.ToLowerInvariant();
    }

    private static string NormalizeRefType(string refName, string refType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refName);
        var expectedPrefix = refType switch
        {
            "branch" => "refs/heads/",
            "tag" => "refs/tags/",
            _ => throw new ArgumentException("Ref type must be branch or tag.", nameof(refType))
        };
        if (!refName.StartsWith(expectedPrefix, StringComparison.Ordinal)
            || refName.Length == expectedPrefix.Length
            || refName.Contains("^{", StringComparison.Ordinal))
        {
            throw new ArgumentException("Ref name does not match its ref type.", nameof(refName));
        }

        return refType;
    }

    private static RepositoryReference NormalizeRepository(RepositoryReference repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository.Provider);
        var provider = repository.Provider.ToLowerInvariant();
        if (provider is not ("github" or "azdo" or "git"))
        {
            throw new ArgumentException("Repository provider must be github, azdo, or git.", nameof(repository));
        }

        var cloneUrl = CloneUrlSanitizer.Sanitize(repository.CloneUrl);
        var canonicalId = CanonicalRepositoryId.FromRemote(cloneUrl).Value;
        return repository with { Provider = provider, CloneUrl = cloneUrl, CanonicalId = canonicalId };
    }

    private static void ValidateIngestion(IngestionInfo ingestion)
    {
        if (ingestion.Mode is not ("webhook" or "poll"))
        {
            throw new ArgumentException("Ingestion mode must be webhook or poll.", nameof(ingestion));
        }
    }

    private static string NormalizePullRequestRef(string refName, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refName, parameterName);
        if (refName.StartsWith("refs/heads/", StringComparison.Ordinal))
        {
            if (refName.Length == "refs/heads/".Length)
            {
                throw new ArgumentException("Pull request branch refs must include a branch name.", parameterName);
            }

            return refName;
        }

        if (refName.StartsWith("refs/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Pull request refs must be branch refs.", parameterName);
        }

        return $"refs/heads/{refName}";
    }

    private static GitSignal CreateSignal(
        IngestionInfo ingestion,
        string type,
        string subject,
        string source,
        GitSignalData data,
        string key,
        string? targetRef = null)
    {
        var signal = new GitSignal
        {
            Id = Guid.CreateVersion7().ToString("D"),
            Source = source,
            Type = type,
            Subject = subject,
            Time = ingestion.ObservedAt,
            Idempotencykey = key,
            Data = data,
            Targetref = targetRef
        };
        return signal;
    }
}
