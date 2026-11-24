using System.Text.Json;
using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Normalization;

namespace GitOpsRelay.Core.Tests;

public sealed class PullRequestTests
{
    [Fact]
    public void CreatePullRequestSignalNormalizesShaAndPromotesTargetRef()
    {
        var signal = new GitSignalNormalizer().CreatePullRequestSignal(
            TestData.Repository(),
            TestData.Ingestion("webhook"),
            new PullRequestInfo
            {
                Id = "pr-42",
                Number = 42,
                Title = "Update dependencies",
                Status = "open",
                IsDraft = false,
                SourceRef = "refs/heads/feature",
                TargetRef = "refs/heads/main",
                HeadSha = TestData.Sha256.ToUpperInvariant()
            });
        var json = JsonSerializer.SerializeToElement(signal, GitSignalJsonContext.Default.GitSignal);

        Assert.Equal(GitSignalTypes.PullRequestOpened, signal.Type);
        Assert.Equal("pr/42", signal.Subject);
        Assert.Equal(TestData.Sha256, signal.Data.PullRequest!.HeadSha);
        Assert.Equal("refs/heads/feature", signal.Data.PullRequest.SourceRef);
        Assert.Equal("refs/heads/main", signal.Data.PullRequest.TargetRef);
        Assert.Equal("refs/heads/main", signal.Targetref);
        Assert.Equal("refs/heads/main", json.GetProperty("targetref").GetString());
        Assert.StartsWith("v1-", signal.Idempotencykey, StringComparison.Ordinal);
        Assert.NotEqual(signal.Id, signal.Idempotencykey);
    }

    [Fact]
    public void PullRequestIdempotencyKeyTracksMutableAttributesAndStatusMustMatchEvent()
    {
        var normalizer = new GitSignalNormalizer();
        var baseline = OpenPullRequest();
        var draft = normalizer.CreatePullRequestSignal(
            TestData.Repository(), TestData.Ingestion(), baseline with { IsDraft = true },
            eventType: GitSignalTypes.PullRequestUpdated);
        var retitled = normalizer.CreatePullRequestSignal(
            TestData.Repository(), TestData.Ingestion(), baseline with { Title = "Retitled" },
            eventType: GitSignalTypes.PullRequestUpdated);
        var retargeted = normalizer.CreatePullRequestSignal(
            TestData.Repository(), TestData.Ingestion(), baseline with { TargetRef = "release" },
            eventType: GitSignalTypes.PullRequestUpdated);

        Assert.NotEqual(draft.Idempotencykey, retitled.Idempotencykey);
        Assert.NotEqual(retitled.Idempotencykey, retargeted.Idempotencykey);
        Assert.Throws<ArgumentException>(() => normalizer.CreatePullRequestSignal(
            TestData.Repository(), TestData.Ingestion(), baseline with { Status = "closed" },
            eventType: GitSignalTypes.PullRequestUpdated));
    }

    private static PullRequestInfo OpenPullRequest() => new()
    {
        Id = "pr-42",
        Number = 42,
        Title = "Update dependencies",
        Status = "open",
        IsDraft = false,
        SourceRef = "feature",
        TargetRef = "main",
        HeadSha = TestData.Sha1
    };
}
