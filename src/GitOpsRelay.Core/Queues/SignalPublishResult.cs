namespace GitOpsRelay.Core.Queues;

public sealed record SignalPublishResult(
    bool Published,
    string? MessageId = null,
    string? Error = null);
