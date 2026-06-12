using System.Text.RegularExpressions;
using Sentry;

namespace EncryptedChat.Client.Observability;

/// <summary>
/// E2E app: the browser holds decrypted messages + crypto keys, so scrubbing is aggressive.
/// Pure/static so it is unit-testable without the Sentry pipeline.
/// </summary>
public static class ClientSentryScrubbing
{
    public static SentryEvent? ScrubEvent(SentryEvent e)
    {
        if (e.Request is { } req && req.Url is { Length: > 0 } url)
            req.Url = StripToken(url);

        if (e.User is { } user)
        {
            user.Email = null;
            user.Username = null;
            user.IpAddress = null;
        }

        return e;
    }

    public static Breadcrumb? ScrubBreadcrumb(Breadcrumb b)
    {
        // Console logs can carry decrypted content → drop them entirely.
        if (string.Equals(b.Category, "console", StringComparison.OrdinalIgnoreCase))
            return null;
        return b;
    }

    public static string StripToken(string input) =>
        Regex.Replace(input, @"access_token=[^&\s]*", "access_token=[Filtered]", RegexOptions.IgnoreCase);
}
