namespace GitOpsRelay.Core.State;

public enum SignalDeduplicationStatus
{
    Reserved,
    DuplicateCommitted,
    InFlight
}
