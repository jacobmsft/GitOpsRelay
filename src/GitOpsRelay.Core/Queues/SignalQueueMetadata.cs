namespace GitOpsRelay.Core.Queues;

public sealed record SignalQueueMetadata(
    string Provider,
    string Repository,
    string EventType,
    string? Ref,
    string IdempotencyKey,
    string? TargetRef);
