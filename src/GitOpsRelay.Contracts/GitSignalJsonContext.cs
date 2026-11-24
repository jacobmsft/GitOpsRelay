using System.Text.Json.Serialization;

namespace GitOpsRelay.Contracts;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(GitSignal))]
[JsonSerializable(typeof(GitSignalData))]
[JsonSerializable(typeof(RepositoryReference))]
[JsonSerializable(typeof(IngestionInfo))]
[JsonSerializable(typeof(RefChangeInfo))]
[JsonSerializable(typeof(PullRequestInfo))]
[JsonSerializable(typeof(SubscriptionFilter))]
public partial class GitSignalJsonContext : JsonSerializerContext;
