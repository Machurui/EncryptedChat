using EncryptedChat.Client.Services;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class NotificationPolicyTests
{
    // pref, isDm, isMentioned, status, isOwn, isMuted -> expected
    [Theory]
    [InlineData("all", false, false, "online", false, false, true)]   // all + online -> toast
    [InlineData("all", false, false, "busy", false, false, false)]    // DnD cuts toast
    [InlineData("none", true, true, "online", false, false, false)]   // none never toasts
    [InlineData("mentions", true, false, "online", false, false, true)]   // DM counts as mention path
    [InlineData("mentions", false, true, "online", false, false, true)]   // explicit mention
    [InlineData("mentions", false, false, "online", false, false, false)] // team, no mention -> no toast
    [InlineData("all", false, false, "online", true, false, false)]   // own message never toasts
    [InlineData("all", false, false, "online", false, true, false)]   // muted never toasts
    public void ShouldShowToast_Matrix(string pref, bool isDm, bool isMentioned, string status, bool isOwn, bool isMuted, bool expected)
    {
        NotificationPolicy.ShouldShowToast(pref, isDm, isMentioned, status, isOwn, isMuted)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("online", true)]
    [InlineData("away", true)]
    [InlineData("busy", false)]
    public void DndEquivalentToNone_ForToast(string status, bool allToasts)
    {
        // 'busy' (DnD) must suppress toasts exactly like 'none' does.
        bool none = NotificationPolicy.ShouldShowToast("none", false, false, status, false, false);
        bool all = NotificationPolicy.ShouldShowToast("all", false, false, status, false, false);
        none.Should().BeFalse();
        all.Should().Be(allToasts);
    }
}
