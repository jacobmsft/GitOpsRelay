using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Normalization;
using GitOpsRelay.Core.Queues;

namespace GitOpsRelay.Core.Tests;

public sealed class SignalQueueLeaseTests
{
    [Fact]
    public void DeserializeAcceptsKnownCloudEventsSignalShape()
    {
        var signal = new GitSignalNormalizer().CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", null, TestData.Sha1)[1];
        var lease = new TestLease(JsonSerializer.SerializeToUtf8Bytes(
            signal, GitSignalJsonContext.Default.GitSignal));

        Assert.Equal(signal.Idempotencykey, ((ISignalMessageLease)lease).Deserialize().Idempotencykey);
    }

    [Fact]
    public void DeserializeRejectsWrongCloudEventsVersionAndMalformedSignalShape()
    {
        var signal = new GitSignalNormalizer().CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", null, TestData.Sha1)[1];
        var json = JsonSerializer.Serialize(signal, GitSignalJsonContext.Default.GitSignal)
            .Replace("\"specversion\":\"1.0\"", "\"specversion\":\"0.3\"", StringComparison.Ordinal);
        var lease = new TestLease(Encoding.UTF8.GetBytes(json));

        Assert.Throws<JsonException>(() => ((ISignalMessageLease)lease).Deserialize());
    }

    [Theory]
    [InlineData("Foo", "1")]
    [InlineData("custom", "{}")]
    [InlineData("custom", "1.5")]
    [InlineData("data_base64", "\"encoded\"")]
    [InlineData("customextensionnameislongerthan20", "1")]
    public void DeserializeWrapsInvalidExtensionAsJsonException(string name, string valueJson)
    {
        var signal = new GitSignalNormalizer().CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", null, TestData.Sha1)[1];
        var json = JsonNode.Parse(JsonSerializer.Serialize(
            signal, GitSignalJsonContext.Default.GitSignal))!.AsObject();
        json[name] = JsonNode.Parse(valueJson);
        var lease = new TestLease(Encoding.UTF8.GetBytes(json.ToJsonString()));

        var exception = Assert.Throws<JsonException>(() => ((ISignalMessageLease)lease).Deserialize());
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private sealed class TestLease(byte[] rawPayload) : ISignalMessageLease
    {
        public string MessageId => "message-1";
        public string? SessionId => null;
        public string ContentType => SignalQueueMessage.CloudEventsContentType;
        public DateTimeOffset EnqueuedAt => DateTimeOffset.UtcNow;
        public DateTimeOffset? LockedUntil => null;
        public IReadOnlyDictionary<string, JsonElement> Properties { get; } =
            new Dictionary<string, JsonElement>();
        public ReadOnlyMemory<byte> RawPayload => rawPayload;
        public int DeliveryCount => 1;
        public ValueTask CompleteAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask AbandonAsync(TimeSpan? retryDelay, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask DeadLetterAsync(string reason, string? description, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
        public ValueTask<DateTimeOffset?> RenewLockAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<DateTimeOffset?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
