using System.Security.Claims;
using EncryptedChat.Controllers;
using EncryptedChat.Hubs;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace EncryptedChat.Tests;

public class FriendControllerTests
{
    private readonly Mock<IFriendService> _mockFriendService = new();
    private readonly Mock<IUserService> _mockUserService = new();
    private readonly Mock<IHubContext<ChatHub>> _mockHub = new();
    private readonly Mock<IHubClients> _mockClients = new();
    private readonly Mock<IClientProxy> _mockUserChannel = new();   // Clients.User(...)
    private readonly Mock<IClientProxy> _mockUsersChannel = new();  // Clients.Users(...)
    private readonly string _userId = Guid.NewGuid().ToString();
    private readonly string _friendId = Guid.NewGuid().ToString();

    public FriendControllerTests()
    {
        _mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(_mockUserChannel.Object);
        _mockClients.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>())).Returns(_mockUsersChannel.Object);
        _mockHub.Setup(h => h.Clients).Returns(_mockClients.Object);
    }

    private FriendController CreateController(string? userId = null)
    {
        var controller = new FriendController(_mockFriendService.Object, _mockUserService.Object, _mockHub.Object, Mock.Of<ILogger<FriendController>>());
        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task RemoveFriend_BroadcastsTeamDeleted_ToBothUsers_WhenDmDeleted()
    {
        var dmId = Guid.NewGuid();
        _mockFriendService
            .Setup(s => s.RemoveFriendAsync(_userId, _friendId))
            .ReturnsAsync((true, _friendId, (Guid?)dmId));

        var controller = CreateController(_userId);
        var result = await controller.RemoveFriend(_friendId);

        result.Should().BeOfType<NoContentResult>();

        // FriendRemoved event sent to the OTHER user
        _mockClients.Verify(c => c.User(_friendId), Times.Once);
        _mockUserChannel.Verify(p => p.SendCoreAsync(
            "FriendRemoved",
            It.Is<object?[]>(args => args.Length == 1 && (string)args[0]! == _userId),
            It.IsAny<CancellationToken>()), Times.Once);

        // TeamDeleted sent to BOTH users
        _mockClients.Verify(c => c.Users(It.Is<IReadOnlyList<string>>(list =>
            list.Count == 2 && list.Contains(_userId) && list.Contains(_friendId))), Times.Once);
        _mockUsersChannel.Verify(p => p.SendCoreAsync(
            "TeamDeleted",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveFriend_DoesNotBroadcastTeamDeleted_WhenNoDmExists()
    {
        _mockFriendService
            .Setup(s => s.RemoveFriendAsync(_userId, _friendId))
            .ReturnsAsync((true, _friendId, (Guid?)null));

        var controller = CreateController(_userId);
        var result = await controller.RemoveFriend(_friendId);

        result.Should().BeOfType<NoContentResult>();

        // FriendRemoved still goes out
        _mockUserChannel.Verify(p => p.SendCoreAsync(
            "FriendRemoved",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // TeamDeleted NOT broadcast
        _mockUsersChannel.Verify(p => p.SendCoreAsync(
            "TeamDeleted",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
