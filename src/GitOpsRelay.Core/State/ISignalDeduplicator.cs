namespace GitOpsRelay.Core.State;

/// <summary>
/// Deduplication implementations must distinguish a committed duplicate from an
/// in-flight reservation. Callers must not advance a snapshot while a key is in flight.
/// </summary>
public interface ISignalDeduplicator
{
    ValueTask<SignalDeduplicationResult> TryReserveAsync(
        string idempotencyKey,
        TimeSpan reservationTtl,
        TimeSpan retentionWindow,
        CancellationToken cancellationToken);
}
