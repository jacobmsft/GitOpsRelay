namespace GitOpsRelay.Core.State;

public interface IDistributedPollLease : IAsyncDisposable
{
    CancellationToken LeaseLostToken { get; }

    ValueTask<bool> RenewAsync(TimeSpan duration, CancellationToken cancellationToken);
}
