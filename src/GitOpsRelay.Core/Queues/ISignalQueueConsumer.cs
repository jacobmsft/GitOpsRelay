namespace GitOpsRelay.Core.Queues;

public interface ISignalQueueConsumer
{
    /// <summary>Capabilities supported by this provider instance.</summary>
    SignalQueueCapabilities Capabilities { get; }

    ValueTask<IReadOnlyList<ISignalMessageLease>> ReceiveAsync(
        int maximumMessages,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default);
}
