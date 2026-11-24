namespace GitOpsRelay.Contracts;

public sealed record IngestionInfo
{
    public required string Mode { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
    public string? RawDeliveryId { get; init; }
}
