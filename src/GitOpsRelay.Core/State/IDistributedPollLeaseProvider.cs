namespace GitOpsRelay.Core.State;

public interface IDistributedPollLeaseProvider
{
    ValueTask<IDistributedPollLease?> TryAcquireAsync(
        string canonicalRepositoryId,
        TimeSpan duration,
        CancellationToken cancellationToken);
}
