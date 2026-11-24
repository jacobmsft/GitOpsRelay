namespace GitOpsRelay.Core.State;

public sealed record SignalDeduplicationResult(
    SignalDeduplicationStatus Status,
    ISignalDeduplicationReservation? Reservation = null,
    TimeSpan? RetryAfter = null);
