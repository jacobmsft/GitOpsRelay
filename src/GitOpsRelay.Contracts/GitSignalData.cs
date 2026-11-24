namespace GitOpsRelay.Contracts;

public sealed record GitSignalData
{
    public required RepositoryReference Repository { get; init; }
    public required IngestionInfo Ingestion { get; init; }
    public RefChangeInfo? Change { get; init; }
    public PullRequestInfo? PullRequest { get; init; }
}
