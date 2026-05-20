using EncryptedChat.Services;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class PresenceServiceTests
{
    [Fact]
    public void IsOnline_UnknownUser_ReturnsFalse()
    {
        var svc = new PresenceService();
        svc.IsOnline("user1").Should().BeFalse();
    }

    [Fact]
    public void IsOnline_EmptyOrNullUserId_ReturnsFalse()
    {
        var svc = new PresenceService();
        svc.IsOnline("").Should().BeFalse();
        svc.IsOnline("   ").Should().BeFalse();
    }

    [Fact]
    public void AddConnection_ThenIsOnline_ReturnsTrue()
    {
        var svc = new PresenceService();
        svc.AddConnection("user1", "conn1");
        svc.IsOnline("user1").Should().BeTrue();
    }

    [Fact]
    public void RemoveConnection_NotLast_ReturnsFalse_AndStaysOnline()
    {
        var svc = new PresenceService();
        svc.AddConnection("user1", "conn1");
        svc.AddConnection("user1", "conn2");

        bool wasLast = svc.RemoveConnection("user1", "conn1");

        wasLast.Should().BeFalse();
        svc.IsOnline("user1").Should().BeTrue();
    }

    [Fact]
    public void RemoveConnection_LastConnection_ReturnsTrue_AndGoesOffline()
    {
        var svc = new PresenceService();
        svc.AddConnection("user1", "conn1");

        bool wasLast = svc.RemoveConnection("user1", "conn1");

        wasLast.Should().BeTrue();
        svc.IsOnline("user1").Should().BeFalse();
    }

    [Fact]
    public void RemoveConnection_UnknownUser_ReturnsFalse()
    {
        var svc = new PresenceService();
        svc.RemoveConnection("user1", "conn1").Should().BeFalse();
    }
}
