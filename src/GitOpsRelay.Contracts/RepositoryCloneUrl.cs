using System.Globalization;

namespace GitOpsRelay.Contracts;

public static class RepositoryCloneUrl
{
    public static string Sanitize(string cloneUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloneUrl);
        if (Uri.TryCreate(cloneUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "ssh")
        {
            var uriUserName = uri.Scheme == "ssh"
                && string.Equals(uri.UserInfo.Split(':')[0], "git", StringComparison.Ordinal)
                ? "git"
                : string.Empty;
            var builder = new UriBuilder(uri)
            {
                UserName = uriUserName,
                Password = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty
            };
            if (string.IsNullOrWhiteSpace(builder.Host) || uri.AbsolutePath == "/")
            {
                throw new ArgumentException("The clone URL must identify a repository.", nameof(cloneUrl));
            }

            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        if (cloneUrl.Contains("://", StringComparison.Ordinal))
        {
            throw new ArgumentException("The clone URL is not a valid supported URL.", nameof(cloneUrl));
        }

        var firstColon = cloneUrl.IndexOf(':');
        var atSign = cloneUrl.IndexOf('@');
        var colonAfterUser = atSign >= 0 ? cloneUrl.IndexOf(':', atSign + 1) : -1;
        var colon = colonAfterUser >= 0
            ? colonAfterUser
            : firstColon;
        if (colon <= 0 || colon == cloneUrl.Length - 1 || cloneUrl[..colon].Contains('/'))
        {
            throw new ArgumentException("The clone URL must be an absolute URL or SCP-style SSH URL.", nameof(cloneUrl));
        }
        if (atSign > colon)
        {
            throw new ArgumentException("The SCP-style SSH clone URL is invalid.", nameof(cloneUrl));
        }

        var authority = cloneUrl[..colon];
        var path = cloneUrl[(colon + 1)..];
        var firstPathSegmentEnd = path.IndexOf('/');
        var firstPathSegment = firstPathSegmentEnd < 0 ? path : path[..firstPathSegmentEnd];
        if (firstPathSegment.Contains('@') || firstPathSegment.Contains(':'))
        {
            throw new ArgumentException("Credentials and unsupported schemes are not valid in SCP-style SSH URLs.", nameof(cloneUrl));
        }
        var at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            var userInfo = authority[..at];
            var host = authority[(at + 1)..];
            authority = userInfo.Contains(':', StringComparison.Ordinal)
                ? host
                : $"{userInfo}@{host}";
        }

        var userName = authority.Contains('@', StringComparison.Ordinal)
            ? authority[..authority.IndexOf('@')]
            : null;
        var hostName = userName is null ? authority : authority[(authority.IndexOf('@') + 1)..];
        try
        {
            hostName = new IdnMapping().GetAscii(hostName);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("The SCP-style SSH host is invalid.", nameof(cloneUrl), exception);
        }

        if (hostName.Length == 0 || userName?.Contains(':', StringComparison.Ordinal) == true
            || authority.Count(character => character == '@') > 1
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains('#') || path.Contains('?') || path.Contains('\\')
            || path.Any(char.IsWhiteSpace) || Uri.CheckHostName(hostName) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("The SCP-style SSH clone URL is invalid.", nameof(cloneUrl));
        }

        authority = userName is null ? hostName : $"{userName}@{hostName}";

        return $"{authority}:{path}";
    }
}
