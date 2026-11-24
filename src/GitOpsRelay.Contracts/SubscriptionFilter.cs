namespace GitOpsRelay.Contracts;

public sealed record SubscriptionFilter
{
    public IReadOnlyList<string>? RepositoryIds { get; init; }
    public IReadOnlyList<string>? EventTypes { get; init; }
    public IReadOnlyList<string>? RefGlobs { get; init; }
    public IReadOnlyList<string>? PathGlobs { get; init; }
}
