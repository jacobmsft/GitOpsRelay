using System.Text.Json;
using GitOpsRelay.Contracts;
using GitOpsRelay.Core.Normalization;
using GitOpsRelay.Core.Polling;

namespace GitOpsRelay.Core.Tests;

public sealed class NormalizationTests
{
    private readonly GitSignalNormalizer _normalizer = new();

    [Fact]
    public async Task ZeroObjectIdWebhookMatchesPollerCreateTransition()
    {
        var zero = new string('0', 40);
        var webhookSignals = _normalizer.CreateRefTransitionSignals(
            TestData.Repository(),
            TestData.Ingestion("webhook"),
            "refs/heads/main",
            "branch",
            zero,
            TestData.Sha1);
        var pollSignals = await new RefSnapshotDiffEngine(_normalizer).DiffAsync(
            TestData.Repository(),
            TestData.Ingestion(),
            null,
            new Dictionary<string, string> { ["refs/heads/main"] = TestData.Sha1 },
            BaselineSeedMode.EmitExisting);

        Assert.Equal(
            webhookSignals.Select(signal => (signal.Type, signal.Idempotencykey)),
            pollSignals.Select(signal => (signal.Type, signal.Idempotencykey)));
        Assert.Null(webhookSignals[0].Data.Change!.BeforeSha);
        Assert.Null(webhookSignals[0].Data.Change!.IsForcePush);
    }

    [Fact]
    public async Task DiffIgnoresPeeledAnnotatedTagEntries()
    {
        var peeled = new Dictionary<string, string>
        {
            ["refs/tags/v1"] = TestData.Sha1,
            ["refs/tags/v1^{}"] = TestData.Sha1Next
        };
        var unchanged = await new RefSnapshotDiffEngine(_normalizer).DiffAsync(
            TestData.Repository(),
            TestData.Ingestion(),
            peeled,
            new Dictionary<string, string>(peeled)
            {
                ["refs/tags/v1^{}"] = TestData.Sha256
            },
            BaselineSeedMode.Silent);

        Assert.Empty(unchanged);

        var changed = await new RefSnapshotDiffEngine(_normalizer).DiffAsync(
            TestData.Repository(),
            TestData.Ingestion(),
            peeled,
            new Dictionary<string, string>(peeled)
            {
                ["refs/tags/v1"] = TestData.Sha1Next
            },
            BaselineSeedMode.Silent);

        var signal = Assert.Single(changed);
        Assert.Equal(GitSignalTypes.Push, signal.Type);
        Assert.Equal("refs/tags/v1", signal.Data.Change!.Ref);
        Assert.Equal(TestData.Sha1, signal.Data.Change.BeforeSha);
        Assert.Equal(TestData.Sha1Next, signal.Data.Change.AfterSha);
    }

    [Fact]
    public async Task DiffIgnoresNonBranchAndNonTagRefs()
    {
        var oldRefs = new Dictionary<string, string>
        {
            ["HEAD"] = TestData.Sha1,
            ["refs/pull/42/head"] = TestData.Sha1,
            ["refs/changes/12/123/head"] = TestData.Sha1,
            ["refs/notes/commits"] = TestData.Sha1,
            ["refs/heads/main"] = TestData.Sha1
        };
        var newRefs = new Dictionary<string, string>(oldRefs)
        {
            ["HEAD"] = TestData.Sha1Next,
            ["refs/pull/42/head"] = TestData.Sha1Next,
            ["refs/changes/12/123/head"] = TestData.Sha1Next,
            ["refs/notes/commits"] = TestData.Sha1Next
        };
        var signals = await new RefSnapshotDiffEngine(_normalizer).DiffAsync(
            TestData.Repository(), TestData.Ingestion(), oldRefs, newRefs, BaselineSeedMode.Silent);

        Assert.Empty(signals);
        Assert.Equal(["refs/heads/main"], RefSnapshotDiffEngine.NormalizeSnapshot(newRefs).Keys);
    }

    [Fact]
    public async Task InitialBaselineCanBeSeededSilently()
    {
        var initialRefs = new Dictionary<string, string>
        {
            ["refs/heads/main"] = TestData.Sha1,
            ["refs/tags/v1"] = TestData.Sha1Next
        };

        var silent = await new RefSnapshotDiffEngine(_normalizer).DiffAsync(
            TestData.Repository(),
            TestData.Ingestion(),
            null,
            initialRefs,
            BaselineSeedMode.Silent);
        var emitted = await new RefSnapshotDiffEngine(_normalizer).DiffAsync(
            TestData.Repository(),
            TestData.Ingestion(),
            null,
            initialRefs,
            BaselineSeedMode.EmitExisting);

        Assert.Empty(silent);
        Assert.Equal(4, emitted.Count);
    }

    [Fact]
    public void CanonicalRepositoryIdNormalizesProvidersSchemesUserInfoCasingAndGitSuffix()
    {
        var httpsId = CanonicalRepositoryId.FromRemote("https://github.com/Octo/App.git");
        var sshId = CanonicalRepositoryId.FromRemote("git@github.com:octo/app.git");
        var credentialedId = CanonicalRepositoryId.FromRemote("https://user:secret@github.com/octo/app");

        Assert.Equal("github.com/octo/app", httpsId.Value);
        Assert.Equal(httpsId, sshId);
        Assert.Equal(httpsId, credentialedId);
    }

    [Fact]
    public void CanonicalRepositoryIdNormalizesUnicodeToNfc()
    {
        var decomposed = CanonicalRepositoryId.FromRemote("https://github.com/Org/e\u0301.git");
        var composed = CanonicalRepositoryId.FromRemote("https://github.com/Org/é.git");

        Assert.Equal(composed, decomposed);
    }

    [Theory]
    [InlineData("https://dev.azure.com/Contoso/Project/_git/Widget.git", "dev.azure.com/contoso/project/widget")]
    [InlineData("https://Contoso.visualstudio.com/Project/_git/Widget", "dev.azure.com/contoso/project/widget")]
    [InlineData("https://Contoso.visualstudio.com/DefaultCollection/Project/_git/Widget", "dev.azure.com/contoso/project/widget")]
    [InlineData("https://dev.azure.com/Contoso/DefaultCollection/Project/_git/Widget", "dev.azure.com/contoso/project/widget")]
    [InlineData("git@ssh.dev.azure.com:v3/Contoso/Project/Widget", "dev.azure.com/contoso/project/widget")]
    [InlineData("ssh://git@ssh.dev.azure.com/v3/Contoso/Project/Widget", "dev.azure.com/contoso/project/widget")]
    [InlineData("Contoso@vs-ssh.visualstudio.com:v3/Contoso/Project/Widget", "dev.azure.com/contoso/project/widget")]
    [InlineData("https://dev.azure.com/Contoso/_git/Widget", "dev.azure.com/contoso/widget/widget")]
    [InlineData("ssh://git@ssh.github.com:443/Owner/Repo.git", "github.com/owner/repo")]
    [InlineData("ssh.github.com:443/Owner/Repo.git", "github.com/owner/repo")]
    [InlineData("git@github.com:Owner/Repo.git", "github.com/owner/repo")]
    public void CanonicalRepositoryIdRecognizesProviderUrlVariants(string remote, string expected) =>
        Assert.Equal(expected, CanonicalRepositoryId.FromRemote(remote).Value);

    [Fact]
    public void CanonicalRepositoryIdRejectsEncodedSeparatorsAndDefaultValueAccess()
    {
        Assert.Throws<ArgumentException>(() => CanonicalRepositoryId.FromRemote("https://github.com/org%2Frepo/name"));
        Assert.Throws<InvalidOperationException>(() => _ = default(CanonicalRepositoryId).Value);
    }

    [Fact]
    public void CanonicalRepositoryIdConvertsIdnHostsToAscii()
    {
        Assert.Equal(
            "xn--bcher-kva.example/org/repo",
            CanonicalRepositoryId.FromRemote("https://bücher.example/Org/Repo.git").Value);
    }

    [Fact]
    public void CanonicalRepositoryIdDecodesAzureDevOpsSshPathSpaces()
    {
        var ssh = CanonicalRepositoryId.FromRemote(
            "git@ssh.dev.azure.com:v3/Contoso/My%20Project/My%20Repo");
        var https = CanonicalRepositoryId.FromRemote(
            "https://dev.azure.com/Contoso/My%20Project/_git/My%20Repo");

        Assert.Equal("dev.azure.com/contoso/my project/my repo", ssh.Value);
        Assert.Equal(https, ssh);
    }

    [Theory]
    [InlineData("deploy@host.example:org/repo.git", "deploy@host.example:org/repo.git")]
    [InlineData("host.example:org/repo.git", "host.example:org/repo.git")]
    [InlineData("user:pw@github.com:org/repo.git", "github.com:org/repo.git")]
    public void ScpCloneUrlsRemainCanonicalAndCredentialSafe(string input, string expected) =>
        Assert.Equal(expected, CloneUrlSanitizer.Sanitize(input));

    [Theory]
    [InlineData("deploy@host.example:org/repo.git", "host.example/org/repo")]
    [InlineData("host.example:org/repo.git", "host.example/org/repo")]
    [InlineData("user:pw@github.com:org/repo.git", "github.com/org/repo")]
    public void ScpCloneUrlsHaveCanonicalRepositoryIds(string input, string expected) =>
        Assert.Equal(expected, CanonicalRepositoryId.FromRemote(CloneUrlSanitizer.Sanitize(input)).Value);

    [Theory]
    [InlineData(40)]
    [InlineData(64)]
    public void AllZeroObjectIdsNormalizeToNull(int length)
    {
        Assert.Null(GitSignalNormalizer.NormalizeObjectId(new string('0', length)));
    }

    [Fact]
    public void NormalizerScrubsCloneUrlCredentialsBeforeSerialization()
    {
        var signal = _normalizer.CreateRefTransitionSignals(
            TestData.Repository("https://user:secret@github.com/Octo/App.git"),
            TestData.Ingestion(),
            "refs/heads/main",
            "branch",
            null,
            TestData.Sha1)[0];
        var json = JsonSerializer.Serialize(signal, GitSignalJsonContext.Default.GitSignal);

        Assert.Equal("https://github.com/Octo/App.git", signal.Data.Repository.CloneUrl);
        Assert.DoesNotContain("secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("user:", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://github.com/Octo/App.git?access_token=secret#fragment", "https://github.com/Octo/App.git")]
    [InlineData("ssh://git:secret@github.com/Octo/App.git?token=secret#fragment", "ssh://git@github.com/Octo/App.git")]
    [InlineData("git@github.com:Octo/App.git", "git@github.com:Octo/App.git")]
    [InlineData("user:secret@github.com:Octo/App.git", "github.com:Octo/App.git")]
    public void CloneUrlSanitizerRemovesSecretsAndPreservesGitSshUser(string input, string expected) =>
        Assert.Equal(expected, CloneUrlSanitizer.Sanitize(input));

    [Theory]
    [InlineData("file:///tmp/repo")]
    [InlineData("opaque user:secret@host/repo")]
    [InlineData("git@host")]
    public void CloneUrlSanitizerRejectsUnsupportedOrMalformedInput(string input) =>
        Assert.Throws<ArgumentException>(() => CloneUrlSanitizer.Sanitize(input));

    [Fact]
    public void WithNormalizedIdentitySanitizesCloneUrl()
    {
        var repository = TestData.Repository("https://user:secret@github.com/Octo/App.git?token=secret")
            .WithNormalizedIdentity();

        Assert.Equal("https://github.com/Octo/App.git", repository.CloneUrl);
        Assert.Equal("github.com/octo/app", repository.CanonicalId);
    }

    [Fact]
    public void NormalizerRejectsInvalidRefTypesAndNoOpTransitions()
    {
        Assert.Throws<ArgumentException>(() => _normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/tags/v1", "branch", null, TestData.Sha1));
        Assert.Throws<ArgumentException>(() => _normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", TestData.Sha1, TestData.Sha1));
        Assert.Throws<ArgumentException>(() => _normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch", null, null));
    }

    [Fact]
    public void IdempotencyKeyUsesFixedVersionedNfcLengthPrefixVector()
    {
        Assert.Equal(
            "v1-8b3bff9630005eac9df42233be8bc81f25276ad3369727598ece14b329d70e0e",
            IdempotencyKey.Create((string?)null, "", "e\u0301"));
        Assert.NotEqual(IdempotencyKey.Create((string?)null), IdempotencyKey.Create(""));
    }

    [Fact]
    public void RepeatedTransitionOccurrenceHasUniqueCloudEventsIdAndStableIdempotencyKey()
    {
        var first = _normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch",
            TestData.Sha1, TestData.Sha1Next)[0];
        var repeated = _normalizer.CreateRefTransitionSignals(
            TestData.Repository(), TestData.Ingestion(), "refs/heads/main", "branch",
            TestData.Sha1, TestData.Sha1Next)[0];

        Assert.NotEqual(first.Id, repeated.Id);
        Assert.Equal(first.Idempotencykey, repeated.Idempotencykey);
    }

    [Fact]
    public void NormalizerValidatesProviderAndIngestionMode()
    {
        var invalidProvider = TestData.Repository() with { Provider = "unknown" };
        var invalidIngestion = TestData.Ingestion() with { Mode = "unknown" };
        Assert.Throws<ArgumentException>(() => _normalizer.CreateRefTransitionSignals(
            invalidProvider, TestData.Ingestion(), "refs/heads/main", "branch", null, TestData.Sha1));
        Assert.Throws<ArgumentException>(() => _normalizer.CreateRefTransitionSignals(
            TestData.Repository(), invalidIngestion, "refs/heads/main", "branch", null, TestData.Sha1));
    }
}
