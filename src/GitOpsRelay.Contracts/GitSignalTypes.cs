namespace GitOpsRelay.Contracts;

public static class GitSignalTypes
{
    public const string Push = "io.gitopsrelay.git.push";
    public const string BranchCreated = "io.gitopsrelay.git.branch.created";
    public const string BranchDeleted = "io.gitopsrelay.git.branch.deleted";
    public const string TagCreated = "io.gitopsrelay.git.tag.created";
    public const string TagDeleted = "io.gitopsrelay.git.tag.deleted";
    public const string PullRequestOpened = "io.gitopsrelay.git.pr.opened";
    public const string PullRequestUpdated = "io.gitopsrelay.git.pr.updated";
    public const string PullRequestMerged = "io.gitopsrelay.git.pr.merged";
    public const string PullRequestClosed = "io.gitopsrelay.git.pr.closed";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Push, BranchCreated, BranchDeleted, TagCreated, TagDeleted,
        PullRequestOpened, PullRequestUpdated, PullRequestMerged, PullRequestClosed
    };
}
