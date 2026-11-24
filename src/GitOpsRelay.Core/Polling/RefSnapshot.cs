namespace GitOpsRelay.Core.Polling;

public sealed record RefSnapshot(
    string CanonicalRepositoryId,
    string RefName,
    string? ObjectId,
    string Version)
{
    public bool IsActive => ObjectId is not null;
}
