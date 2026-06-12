using EncryptedChat.Client.Observability;
using FluentAssertions;
using Sentry;

namespace EncryptedChat.Tests;

public class ClientSentryScrubbingTests
{
    [Fact]
    public void ScrubBreadcrumb_drops_console_category()
    {
        var b = new Breadcrumb("decrypted: hello", "default", category: "console");
        ClientSentryScrubbing.ScrubBreadcrumb(b).Should().BeNull();
    }

    [Fact]
    public void ScrubBreadcrumb_keeps_non_console()
    {
        var b = new Breadcrumb("navigated", "navigation", category: "navigation");
        ClientSentryScrubbing.ScrubBreadcrumb(b).Should().NotBeNull();
    }

    [Fact]
    public void ScrubEvent_strips_token_in_url_and_pii_but_keeps_userid()
    {
        var e = new SentryEvent
        {
            Request = new SentryRequest { Url = "https://api/x?access_token=SECRET" },
            User = new SentryUser { Id = "g", Email = "a@b.c" },
        };

        ClientSentryScrubbing.ScrubEvent(e);

        e.Request.Url.Should().NotContain("SECRET");
        e.User!.Email.Should().BeNull();
        e.User!.Id.Should().Be("g");
    }
}
