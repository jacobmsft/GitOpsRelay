namespace GitOpsRelay.Core.Queues;

public interface ISignalQueuePublisher
{
    /// <summary>Capabilities supported by this provider instance.</summary>
    SignalQueueCapabilities Capabilities { get; }

    ValueTask<SignalPublishResult> PublishAsync(
        SignalQueueMessage message,
        CancellationToken cancellationToken);

    /// <summary>Returns one result per item in the same order as the request.</summary>
    ValueTask<IReadOnlyList<SignalPublishResult>> PublishBatchAsync(
        IReadOnlyCollection<SignalQueueMessage> messages,
        CancellationToken cancellationToken);
}
