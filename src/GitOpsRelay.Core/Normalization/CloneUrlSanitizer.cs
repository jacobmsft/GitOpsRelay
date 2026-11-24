using GitOpsRelay.Contracts;

namespace GitOpsRelay.Core.Normalization;

public static class CloneUrlSanitizer
{
    public static string Sanitize(string cloneUrl) => RepositoryCloneUrl.Sanitize(cloneUrl);
}
