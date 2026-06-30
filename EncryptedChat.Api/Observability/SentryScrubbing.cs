using System.Text.RegularExpressions;
using Sentry;

namespace EncryptedChat.Observability;

/// <summary>
/// Removes auth secrets, request bodies and PII from a Sentry event before it leaves the server.
/// Pure/static so it is unit-testable without spinning up the Sentry pipeline.
/// </summary>
public static partial class SentryScrubbing
{
    public static SentryEvent? ScrubEvent(SentryEvent e)
    {
        if (e.Request is { } req)
        {
            req.Headers?.Remove("Authorization");
            req.Headers?.Remove("Cookie");
            if (req.QueryString is { Length: > 0 } qs) req.QueryString = StripToken(qs);
            if (req.Url is { Length: > 0 } url) req.Url = StripToken(url);
            req.Data = null; // never the body (messages, passwords, recovery phrases, keys)
        }

        if (e.User is { } user)
        {
            user.Email = null;
            user.Username = null;
            user.IpAddress = null;
            // user.Id (pseudonymous GUID) is kept for correlation.
        }

        return e;
    }

    /// Replaces the value of any access_token=... in a query string or URL with a marker.
    public static string StripToken(string input) => MyRegex().Replace(input, "access_token=[Filtered]");
    [GeneratedRegex(@"access_token=[^&\s]*", RegexOptions.IgnoreCase, "fr-FR")]
    private static partial Regex MyRegex();
}
