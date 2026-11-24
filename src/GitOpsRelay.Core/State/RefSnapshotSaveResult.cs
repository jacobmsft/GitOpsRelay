using GitOpsRelay.Core.Polling;

namespace GitOpsRelay.Core.State;

public enum RefSnapshotSaveStatus
{
    Saved,
    ConflictModified,
    ConflictDeleted
}

public sealed record RefSnapshotSaveResult(
    RefSnapshotSaveStatus Status,
    RefSnapshot? Snapshot)
{
    /// <summary>True only when the CAS succeeded and Snapshot contains the updated token.</summary>
    public bool Saved => Status == RefSnapshotSaveStatus.Saved;
}
