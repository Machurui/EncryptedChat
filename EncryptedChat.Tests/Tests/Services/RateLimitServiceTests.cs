using EncryptedChat.Services;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class RateLimitServiceTests
{
    [Fact]
    public void CheckAndRecord_FirstSend_Allowed()
    {
        var svc = new RateLimitService();
        var result = svc.CheckAndRecord("user1");
        result.Allowed.Should().BeTrue();
        result.RetryAfterMs.Should().Be(0);
    }

    [Fact]
    public void CheckAndRecord_TenFastSends_AllAllowed()
    {
        var svc = new RateLimitService();
        for (int i = 0; i < 10; i++)
        {
            var result = svc.CheckAndRecord("user1");
            result.Allowed.Should().BeTrue("send {0} should still be in burst window", i + 1);
        }
    }

    [Fact]
    public void CheckAndRecord_EleventhFastSend_Rejected()
    {
        var svc = new RateLimitService();
        for (int i = 0; i < 10; i++)
            svc.CheckAndRecord("user1");

        var result = svc.CheckAndRecord("user1");
        result.Allowed.Should().BeFalse();
        result.RetryAfterMs.Should().BeGreaterThan(0);
        result.RetryAfterMs.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public void CheckAndRecord_DifferentUsers_IsolatedCounters()
    {
        var svc = new RateLimitService();
        for (int i = 0; i < 10; i++)
            svc.CheckAndRecord("userA");

        var resultB = svc.CheckAndRecord("userB");
        resultB.Allowed.Should().BeTrue();
    }

    [Fact]
    public void CleanupStaleEntries_RemovesOldUsers()
    {
        var svc = new RateLimitService();
        svc.CheckAndRecord("user1");

        Thread.Sleep(100);
        svc.CleanupStaleEntries(TimeSpan.FromMilliseconds(50));

        for (int i = 0; i < 10; i++)
        {
            var result = svc.CheckAndRecord("user1");
            result.Allowed.Should().BeTrue("after cleanup, burst window resets");
        }
    }
}
