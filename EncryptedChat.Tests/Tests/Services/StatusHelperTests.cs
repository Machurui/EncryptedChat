using EncryptedChat.Services;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class StatusHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("online")]
    [InlineData("away")]
    [InlineData("busy")]
    [InlineData("invisible")]
    public void EffectiveStatus_NotConnected_ReturnsOffline_RegardlessOfDb(string? dbStatus)
    {
        StatusHelper.EffectiveStatus(dbStatus, isConnected: false).Should().Be("offline");
    }

    [Fact]
    public void EffectiveStatus_Connected_Invisible_ReturnsOffline()
    {
        StatusHelper.EffectiveStatus("invisible", isConnected: true).Should().Be("offline");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EffectiveStatus_Connected_NullOrEmpty_ReturnsOnline(string? dbStatus)
    {
        StatusHelper.EffectiveStatus(dbStatus, isConnected: true).Should().Be("online");
    }

    [Theory]
    [InlineData("online")]
    [InlineData("away")]
    [InlineData("busy")]
    public void EffectiveStatus_Connected_NormalStatus_ReturnsDbValue(string dbStatus)
    {
        StatusHelper.EffectiveStatus(dbStatus, isConnected: true).Should().Be(dbStatus);
    }
}
