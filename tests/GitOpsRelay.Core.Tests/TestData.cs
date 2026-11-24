using GitOpsRelay.Contracts;

namespace GitOpsRelay.Core.Tests;

internal static class TestData
{
    public const string Sha1 = "0123456789abcdef0123456789abcdef01234567";
    public const string Sha1Next = "89abcdef0123456789abcdef0123456789abcdef";
    public const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    public static RepositoryReference Repository(string cloneUrl = "https://github.com/Octo/App.git") => new()
    {
        Provider = "github",
        Host = "github.com",
        Owner = "Octo",
        Name = "App",
        CloneUrl = cloneUrl,
        CanonicalId = "ignored-by-normalizer"
    };

    public static IngestionInfo Ingestion(string mode = "poll") => new()
    {
        Mode = mode,
        ObservedAt = DateTimeOffset.Parse("2026-11-22T00:00:00Z")
    };
}
