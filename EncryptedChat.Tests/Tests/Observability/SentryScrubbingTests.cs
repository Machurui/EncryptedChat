using EncryptedChat.Observability;
using FluentAssertions;
using Sentry;

namespace EncryptedChat.Tests;

public class SentryScrubbingTests
{
    [Fact]
    public void ScrubEvent_strips_auth_token_body_and_pii_but_keeps_userid()
    {
        var e = new SentryEvent
        {
            Request = new SentryRequest
            {
                Url = "https://x/hubs/chat?access_token=SECRET",
                QueryString = "access_token=SECRET&x=1",
                Data = "{\"password\":\"p\"}",
            },
            User = new SentryUser { Id = "guid-123", Email = "a@b.c", Username = "alice" },
        };
        e.Request.Headers.Add("Authorization", "Bearer JWT");
        e.Request.Headers.Add("Cookie", "ec.accessToken=SECRET");

        SentryScrubbing.ScrubEvent(e);

        e.Request.Headers.Should().NotContainKey("Authorization");
        e.Request.Headers.Should().NotContainKey("Cookie");
        e.Request.QueryString.Should().NotContain("SECRET");
        e.Request.Url.Should().NotContain("SECRET");
        e.Request.Data.Should().BeNull();
        e.User!.Email.Should().BeNull();
        e.User!.Username.Should().BeNull();
        e.User!.Id.Should().Be("guid-123");
    }

    [Fact]
    public void StripToken_filters_access_token_value_only()
    {
        SentryScrubbing.StripToken("a=1&access_token=abc.def&b=2")
            .Should().Be("a=1&access_token=[Filtered]&b=2");
    }
}
