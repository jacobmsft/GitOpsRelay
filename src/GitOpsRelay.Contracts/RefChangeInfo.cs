using System.Text.Json.Serialization;

namespace GitOpsRelay.Contracts;

public sealed record RefChangeInfo
{
    public required string Ref { get; init; }
    public required string RefType { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? BeforeSha { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? AfterSha { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool? IsForcePush { get; init; }
    public IReadOnlyList<string>? ChangedPaths { get; init; }
    public string? Actor { get; init; }
}
