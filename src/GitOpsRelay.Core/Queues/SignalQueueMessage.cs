using System.Security.Cryptography;
using System.Text;
using GitOpsRelay.Contracts;

namespace GitOpsRelay.Core.Queues;

public sealed record SignalQueueMessage
{
    public const string CloudEventsContentType = "application/cloudevents+json";

    public required GitSignal Signal { get; init; }
    public string? RoutingTarget { get; init; }
    public string ContentType => CloudEventsContentType;
    public string OrderingKey => BoundOrderingKey(
        $"{Signal.Data.Repository.CanonicalId}|{Signal.Data.Change?.Ref ?? Signal.Targetref ?? Signal.Subject}");
    public string IdempotencyKey => Signal.Idempotencykey;
    public SignalQueueMetadata PromotedMetadata => new(
        Signal.Data.Repository.Provider,
        Signal.Data.Repository.CanonicalId,
        Signal.Type,
        Signal.Data.Change?.Ref ?? Signal.Data.PullRequest?.SourceRef,
        IdempotencyKey,
        Signal.Targetref ?? Signal.Data.PullRequest?.TargetRef);

    private static string BoundOrderingKey(string value) =>
        value.Length <= 128
            ? value
            : $"v1-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";
}
