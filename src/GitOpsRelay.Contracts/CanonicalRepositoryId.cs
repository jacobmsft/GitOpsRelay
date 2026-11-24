using System.Globalization;
using System.Text;

namespace GitOpsRelay.Contracts;

public readonly record struct CanonicalRepositoryId
{
    private readonly string? _value;

    private CanonicalRepositoryId(string value) => _value = value;

    public string Value => _value
        ?? throw new InvalidOperationException("The default repository ID is not a valid canonical ID.");

    public static CanonicalRepositoryId FromRemote(string remote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        var (host, path) = ParseRemote(remote);
        var asciiHost = new IdnMapping().GetAscii(host.Trim().TrimEnd('.').Normalize(NormalizationForm.FormC));
        var segments = ParsePath(path);
        string normalized;

        if (asciiHost.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            normalized = NormalizeAzurePath(segments);
        }
        else if (asciiHost.Equals("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase)
            || asciiHost.Equals("vs-ssh.visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            normalized = NormalizeAzureSshPath(segments);
        }
        else if (asciiHost.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var organization = asciiHost[..^".visualstudio.com".Length];
            normalized = NormalizeAzurePath(segments, organization);
        }
        else
        {
            var canonicalHost = asciiHost.Equals("ssh.github.com", StringComparison.OrdinalIgnoreCase)
                ? "github.com"
                : asciiHost;
            normalized = $"{canonicalHost}/{string.Join('/', segments.Select(NormalizeSegment))}";
        }

        return new CanonicalRepositoryId(normalized.ToLower(CultureInfo.InvariantCulture));
    }

    public override string ToString() => Value;

    private static (string Host, string Path) ParseRemote(string remote)
    {
        const string githubSshPortPrefix = "ssh.github.com:443/";
        const string githubSshUserPortPrefix = "git@ssh.github.com:443/";
        if (remote.StartsWith(githubSshUserPortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ("github.com", DecodeSafePath(remote[githubSshUserPortPrefix.Length..]));
        }
        if (remote.StartsWith(githubSshPortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ("github.com", DecodeSafePath(remote[githubSshPortPrefix.Length..]));
        }

        if (remote.Contains("://", StringComparison.Ordinal)
            && Uri.TryCreate(remote, UriKind.Absolute, out var uri)
            && uri.IsAbsoluteUri)
        {
            if (uri.Scheme is not ("http" or "https" or "ssh" or "git"))
            {
                throw new ArgumentException("The remote scheme is not supported.", nameof(remote));
            }

            var encodedPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
            var path = DecodeSafePath(encodedPath);
            var host = uri.IdnHost;
            var githubSshAlias = host.Equals("ssh.github.com", StringComparison.OrdinalIgnoreCase)
                && uri.Port == 443;
            if (githubSshAlias)
            {
                host = "github.com";
            }
            else if (!uri.IsDefaultPort)
            {
                host = $"{host}:{uri.Port}";
            }

            return (host, path);
        }

        var firstColon = remote.IndexOf(':');
        var firstAt = remote.IndexOf('@');
        var colonAfterUser = firstAt >= 0 ? remote.IndexOf(':', firstAt + 1) : -1;
        var scpSeparator = colonAfterUser >= 0 ? colonAfterUser : firstColon;
        var slashSeparator = remote.IndexOf('/');
        if (scpSeparator > 0 && (slashSeparator < 0 || scpSeparator < slashSeparator))
        {
            var hostPart = remote[..scpSeparator];
            var at = hostPart.LastIndexOf('@');
            var host = at >= 0 ? hostPart[(at + 1)..] : hostPart;
            if (host.Length == 0
                || Uri.CheckHostName(new IdnMapping().GetAscii(host)) == UriHostNameType.Unknown)
            {
                throw new ArgumentException("The SCP-style SSH host is invalid.", nameof(remote));
            }

            var path = DecodeSafePath(remote[(scpSeparator + 1)..]);
            if (host.Equals("ssh.github.com", StringComparison.OrdinalIgnoreCase)
                && path.StartsWith("443/", StringComparison.Ordinal))
            {
                host = "github.com";
                path = path[4..];
            }

            return (host, path);
        }

        throw new ArgumentException("The remote must be an absolute supported URL or SCP-style SSH URL.", nameof(remote));
    }

    private static string[] ParsePath(string path)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Normalize(NormalizationForm.FormC))
            .ToArray();
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The remote must contain a valid repository path.", nameof(path));
        }

        if (segments[^1].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            segments[^1] = segments[^1][..^4];
        }
        if (segments[^1].Length == 0)
        {
            throw new ArgumentException("The remote must contain a repository name.", nameof(path));
        }

        return segments;
    }

    private static string NormalizeAzurePath(string[] segments, string? knownOrganization = null)
    {
        var offset = 0;
        var organization = knownOrganization;
        if (organization is null)
        {
            if (segments.Length == 0
                || segments[0].Equals("DefaultCollection", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Azure DevOps URL is missing its organization.", nameof(segments));
            }

            organization = segments[offset++];
        }

        if (segments.Length > offset
            && segments[offset].Equals("DefaultCollection", StringComparison.OrdinalIgnoreCase))
        {
            offset++;
        }

        var remaining = segments.Length - offset;
        if (remaining == 3 && segments[offset + 1].Equals("_git", StringComparison.OrdinalIgnoreCase))
        {
            return $"dev.azure.com/{organization}/{NormalizeSegment(segments[offset])}/{NormalizeSegment(segments[offset + 2])}";
        }
        if (remaining == 2 && segments[offset].Equals("_git", StringComparison.OrdinalIgnoreCase))
        {
            var repository = NormalizeSegment(segments[offset + 1]);
            return $"dev.azure.com/{organization}/{repository}/{repository}";
        }
        if (remaining == 2)
        {
            return $"dev.azure.com/{organization}/{NormalizeSegment(segments[offset])}/{NormalizeSegment(segments[offset + 1])}";
        }

        throw new ArgumentException("Azure DevOps URL must identify a project and repository.", nameof(segments));
    }

    private static string NormalizeAzureSshPath(string[] segments)
    {
        if (segments.Length == 4 && segments[0].Equals("v3", StringComparison.OrdinalIgnoreCase))
        {
            return $"dev.azure.com/{NormalizeSegment(segments[1])}/{NormalizeSegment(segments[2])}/{NormalizeSegment(segments[3])}";
        }

        throw new ArgumentException("Azure DevOps SSH URLs must use v3/{organization}/{project}/{repository}.", nameof(segments));
    }

    private static string NormalizeSegment(string segment) => segment.Normalize(NormalizationForm.FormC);

    private static string DecodeSafePath(string path)
    {
        RejectEncodedPathSeparators(path);
        return Uri.UnescapeDataString(path);
    }

    private static void RejectEncodedPathSeparators(string path)
    {
        if (path.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || path.Contains("%5c", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Encoded path separators are not valid in repository remotes.", nameof(path));
        }
    }
}
