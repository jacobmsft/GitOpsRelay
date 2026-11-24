namespace GitOpsRelay.Core.Queues;

[Flags]
public enum SignalQueueCapabilities
{
    None = 0,
    BatchPublish = 1 << 0,
    Routing = 1 << 1,
    OrderedDelivery = 1 << 2,
    Sessions = 1 << 3,
    DelayedRetry = 1 << 4,
    DeadLetter = 1 << 5,
    LeaseRenewal = 1 << 6
}
