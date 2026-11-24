namespace GitOpsRelay.Contracts;

public sealed record RepositoryReference
{
    public required string Provider { get; init; }
    public required string Host { get; init; }
    public string? Owner { get; init; }
    public string? Project { get; init; }
    public required string Name { get; init; }
    public required string CloneUrl { get; init; }
    public string? DefaultBranch { get; init; }
    public required string CanonicalId { get; init; }

    public RepositoryReference WithNormalizedIdentity()
    {
        var sanitizedCloneUrl = RepositoryCloneUrl.Sanitize(CloneUrl);
        return this with
        {
            CloneUrl = sanitizedCloneUrl,
            CanonicalId = CanonicalRepositoryId.FromRemote(sanitizedCloneUrl).Value
        };
    }
}
