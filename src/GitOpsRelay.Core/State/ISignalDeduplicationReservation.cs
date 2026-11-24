namespace GitOpsRelay.Core.State;

/// <summary>
/// Commit marks the key only after publish succeeds. Abandon or disposal releases
/// an uncommitted reservation; implementations must make disposal idempotent.
/// </summary>
public interface ISignalDeduplicationReservation : IAsyncDisposable
{
    ValueTask CommitAsync(CancellationToken cancellationToken);
    ValueTask AbandonAsync(CancellationToken cancellationToken);

    ValueTask IAsyncDisposable.DisposeAsync() => AbandonAsync(CancellationToken.None);
}
