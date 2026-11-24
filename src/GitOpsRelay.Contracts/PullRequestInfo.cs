namespace GitOpsRelay.Contracts;

public sealed record PullRequestInfo
{
    public required string Id { get; init; }
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required bool IsDraft { get; init; }
    public required string SourceRef { get; init; }
    public required string TargetRef { get; init; }
    public required string HeadSha { get; init; }
    public string? MergeCommitSha { get; init; }
}
