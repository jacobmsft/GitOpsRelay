using System.Text.Json;
using GitOpsRelay.Contracts;

namespace GitOpsRelay.Core.Queues;

/// <summary>
/// Disposing an unsettled lease abandons it using the provider's default retry policy.
/// </summary>
public interface ISignalMessageLease : IAsyncDisposable
{
    string MessageId { get; }
    string? SessionId { get; }
    string ContentType { get; }
    DateTimeOffset EnqueuedAt { get; }
    DateTimeOffset? LockedUntil { get; }
    IReadOnlyDictionary<string, JsonElement> Properties { get; }
    ReadOnlyMemory<byte> RawPayload { get; }
    int DeliveryCount { get; }

    GitSignal Deserialize()
    {
        try
        {
            return DeserializeCore();
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("The queue message contains invalid CloudEvents attributes.", exception);
        }
    }

    private GitSignal DeserializeCore()
    {
        var signal = JsonSerializer.Deserialize(RawPayload.Span, GitSignalJsonContext.Default.GitSignal)
            ?? throw new JsonException("The queue message contained an empty GitSignal payload.");
        if (signal.Specversion != "1.0"
            || signal.Datacontenttype != "application/json"
            || string.IsNullOrWhiteSpace(signal.Id)
            || string.IsNullOrWhiteSpace(signal.Source)
            || string.IsNullOrWhiteSpace(signal.Subject)
            || string.IsNullOrWhiteSpace(signal.Idempotencykey)
            || signal.Time == default
            || signal.Data is null
            || signal.Data.Repository is null
            || signal.Data.Ingestion is null
            || signal.Data.Repository.Provider is not ("github" or "azdo" or "git")
            || string.IsNullOrWhiteSpace(signal.Data.Repository.CanonicalId)
            || signal.Data.Ingestion.Mode is not ("webhook" or "poll")
            || !GitSignalTypes.All.Contains(signal.Type, StringComparer.Ordinal))
        {
            throw new JsonException("The queue message is not a valid GitOpsRelay CloudEvents 1.0 signal.");
        }

        var isPullRequest = signal.Type.StartsWith("io.gitopsrelay.git.pr.", StringComparison.Ordinal);
        if (isPullRequest != (signal.Data.PullRequest is not null)
            || isPullRequest == (signal.Data.Change is not null))
        {
            throw new JsonException("The queue message data does not match the signal event type.");
        }
        if (isPullRequest)
        {
            var pullRequest = signal.Data.PullRequest!;
            var validPullRequestStatus = signal.Type switch
            {
                GitSignalTypes.PullRequestOpened or GitSignalTypes.PullRequestUpdated => pullRequest.Status == "open",
                GitSignalTypes.PullRequestMerged => pullRequest.Status == "merged",
                GitSignalTypes.PullRequestClosed => pullRequest.Status == "closed",
                _ => false
            };
            if (!validPullRequestStatus
                || pullRequest.Number < 1
                || !string.Equals(signal.Subject, $"pr/{pullRequest.Number}", StringComparison.Ordinal)
                || signal.Targetref != pullRequest.TargetRef)
            {
                throw new JsonException("The queue message pull request does not match its event type or subject.");
            }
        }
        else
        {
            var change = signal.Data.Change!;
            if (string.IsNullOrWhiteSpace(change.Ref)
                || change.RefType is not ("branch" or "tag")
                || change.RefType == "branch" && !change.Ref.StartsWith("refs/heads/", StringComparison.Ordinal)
                || change.RefType == "tag" && !change.Ref.StartsWith("refs/tags/", StringComparison.Ordinal)
                || change.BeforeSha is null && change.AfterSha is null
                || change.BeforeSha is not null && change.BeforeSha == change.AfterSha
                || !IsValidObjectId(change.BeforeSha)
                || !IsValidObjectId(change.AfterSha)
                || signal.Subject != change.Ref)
            {
                throw new JsonException("The queue message ref change is malformed.");
            }
            if (signal.Type is GitSignalTypes.BranchCreated or GitSignalTypes.TagCreated && change.BeforeSha is not null
                || signal.Type is GitSignalTypes.BranchDeleted or GitSignalTypes.TagDeleted && change.AfterSha is not null)
            {
                throw new JsonException("The queue message create/delete event has an invalid object ID transition.");
            }
        }

        return signal;
    }

    private static bool IsValidObjectId(string? objectId) =>
        objectId is null
        || (objectId.Length is 40 or 64)
        && objectId.All(Uri.IsHexDigit)
        && !objectId.All(character => character == '0');

    ValueTask CompleteAsync(CancellationToken cancellationToken);
    ValueTask AbandonAsync(TimeSpan? retryDelay, CancellationToken cancellationToken);
    ValueTask DeadLetterAsync(string reason, string? description, CancellationToken cancellationToken);
    ValueTask<DateTimeOffset?> RenewLockAsync(CancellationToken cancellationToken);
}
