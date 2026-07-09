using System.Text.RegularExpressions;

namespace EncryptedChat.Client.Observability;

/// <summary>
/// E2E app: the browser holds decrypted messages + crypto keys, so scrubbing is aggressive.
/// Pure/static so it is unit-testable without the Sentry pipeline.
/// </summary>
public static partial class ClientSentryScrubbing
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
        if (string.Equals(b.Category, "console", StringComparison.OrdinalIgnoreCase))
            return null;
        return b;
    }

    public static string StripToken(string input) =>
        MyRegex().Replace(input, "access_token=[Filtered]");

    [GeneratedRegex(@"access_token=[^&\s]*", RegexOptions.IgnoreCase, "fr-FR")]
    private static partial Regex MyRegex();
}
