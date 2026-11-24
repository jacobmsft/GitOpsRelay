using GitOpsRelay.Core.Queues;
using GitOpsRelay.Core.Normalization;

namespace GitOpsRelay.Core.Tests;

public sealed class QueueMessageTests
{
    [Fact]
    public void OrderingAndPromotedMetadataUseCanonicalRepositoryAndPullRequestTarget()
    {
        var signal = new GitSignalNormalizer().CreatePullRequestSignal(
            TestData.Repository(),
            TestData.Ingestion(),
            new GitOpsRelay.Contracts.PullRequestInfo
            {
                Id = "pr-42",
                Number = 42,
                Title = "Update dependencies",
                Status = "open",
                IsDraft = false,
                SourceRef = "refs/heads/feature",
                TargetRef = "refs/heads/main",
                HeadSha = TestData.Sha1
            });
        var message = new SignalQueueMessage { Signal = signal };

        Assert.Equal("github.com/octo/app|refs/heads/main", message.OrderingKey);
        Assert.Equal("github.com/octo/app", message.PromotedMetadata.Repository);
        Assert.Equal("refs/heads/feature", message.PromotedMetadata.Ref);
        Assert.Equal("refs/heads/main", message.PromotedMetadata.TargetRef);
        Assert.Equal(signal.Idempotencykey, message.PromotedMetadata.IdempotencyKey);
        Assert.Equal("application/cloudevents+json", message.ContentType);
        Assert.True(message.OrderingKey.Length <= 128);
    }

    [Fact]
    public void OrderingKeyIsHashedWhenItWouldExceedServiceBusLimit()
    {
        var signal = new GitSignalNormalizer().CreateRefTransitionSignals(
            TestData.Repository($"https://github.com/{new string('a', 180)}/repo.git"),
            TestData.Ingestion(),
            $"refs/heads/{new string('b', 180)}",
            "branch",
            null,
            TestData.Sha1)[0];
        var message = new SignalQueueMessage { Signal = signal };

        Assert.True(message.OrderingKey.Length <= 128);
        Assert.StartsWith("v1-", message.OrderingKey, StringComparison.Ordinal);
    }
}
