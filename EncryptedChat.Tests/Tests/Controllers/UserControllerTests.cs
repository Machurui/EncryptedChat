using EncryptedChat.Controllers;
using EncryptedChat.Hubs;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System.Security.Claims;

namespace EncryptedChat.Tests;

public class UserControllerTests
{
    private const string TestUserId = "user-123";
    private const string OtherUserId = "user-456";

    private static UserController CreateController(Mock<IUserService> mockService, string? userId = TestUserId)
    {
        Mock<IFriendService> mockFriendService = new();
        Mock<IHubContext<ChatHub>> mockHubContext = new();
        Mock<IPresenceService> mockPresenceService = new();
        Mock<IWebHostEnvironment> mockEnv = new();

        mockFriendService
            .Setup(s => s.GetFriendsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<FriendDTO>());

        mockFriendService
            .Setup(s => s.GetPendingRequestUserIdsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<string>());

        mockService
            .Setup(s => s.GetUserTeamsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<UserTeamDTO>());

        mockPresenceService
            .Setup(s => s.IsOnline(It.IsAny<string>()))
            .Returns(false);

        // UpdateMe broadcasts SelfSettingsChanged via Clients.User(...) (and may use
        // Group/Users for status/profile fan-out), so the hub Clients must be mocked.
        Mock<IHubClients> mockClients = new();
        Mock<IClientProxy> mockClientProxy = new();
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockClients.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>())).Returns(mockClientProxy.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        Mock<IUserKeysService> mockUserKeys = new();
        UserController controller = new(mockService.Object, mockFriendService.Object, mockHubContext.Object, mockPresenceService.Object, mockEnv.Object, mockUserKeys.Object);

        List<Claim> claims = [];
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        ClaimsIdentity identity = new(claims, "TestAuth");
        ClaimsPrincipal principal = new(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task GetMe_ReturnsOk_WhenUserExists()
    {
        Mock<IUserService> mockService = new();
        UserProfileDTO userDto = new() { Id = TestUserId, Name = "Test", Email = "test@test.com", Level = 1 };

        mockService
            .Setup(s => s.GetOwnProfileAsync(TestUserId))
            .ReturnsAsync(userDto);

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.GetMe();

        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(userDto);
    }

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenNoUserId()
    {
        Mock<IUserService> mockService = new();
        UserController controller = CreateController(mockService, userId: null);

        IActionResult result = await controller.GetMe();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMe_ReturnsNotFound_WhenUserDoesNotExist()
    {
        Mock<IUserService> mockService = new();

        mockService
            .Setup(s => s.GetOwnProfileAsync(TestUserId))
            .ReturnsAsync((UserProfileDTO?)null);

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.GetMe();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateMe_ReturnsOk_WhenUpdateSucceeds()
    {
        Mock<IUserService> mockService = new();
        UserUpdateDTO updateDto = new() { Name = "NewName" };
        UserProfileDTO updatedUser = new() { Id = TestUserId, Name = "NewName", Email = "test@test.com", Level = 1 };

        mockService
            .Setup(s => s.UpdateAsync(TestUserId, TestUserId, It.IsAny<UserUpdateDTO>()))
            .ReturnsAsync(new UserUpdateResult(UserOperationStatus.Success, updatedUser));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.UpdateMe(updateDto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateMe_ReturnsConflict_WhenNameOrEmailAlreadyExists()
    {
        Mock<IUserService> mockService = new();
        UserUpdateDTO updateDto = new() { Name = "ExistingName" };

        mockService
            .Setup(s => s.UpdateAsync(TestUserId, TestUserId, It.IsAny<UserUpdateDTO>()))
            .ReturnsAsync(new UserUpdateResult(UserOperationStatus.Conflict));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.UpdateMe(updateDto);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateMe_ReturnsBadRequest_WhenValidationFails()
    {
        Mock<IUserService> mockService = new();
        UserUpdateDTO updateDto = new();

        mockService
            .Setup(s => s.UpdateAsync(TestUserId, TestUserId, It.IsAny<UserUpdateDTO>()))
            .ReturnsAsync(new UserUpdateResult(UserOperationStatus.ValidationFailed));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.UpdateMe(updateDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMyTeams_ReturnsOk_WithTeams()
    {
        Mock<IUserService> mockService = new();
        List<UserTeamDTO> teams = [new() { Id = Guid.NewGuid(), Name = "Team1", Slug = "team1", Role = "Admin" }];

        mockService
            .Setup(s => s.GetUserTeamsAsync(TestUserId, TestUserId, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(teams);

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.GetMyTeams();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMyTeams_ReturnsUnauthorized_WhenNoUserId()
    {
        Mock<IUserService> mockService = new();
        UserController controller = CreateController(mockService, userId: null);

        IActionResult result = await controller.GetMyTeams();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsOk_WhenTeammate()
    {
        Mock<IUserService> mockService = new();
        UserDTOPublic userDto = new() { Id = OtherUserId, Name = "Other", Level = 1 };

        mockService
            .Setup(s => s.GetUserAsync(OtherUserId, TestUserId))
            .ReturnsAsync(userDto);

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.GetUser(OtherUserId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsNotFound_WhenNotTeammate()
    {
        Mock<IUserService> mockService = new();

        mockService
            .Setup(s => s.GetUserAsync(OtherUserId, TestUserId))
            .ReturnsAsync((UserDTOPublic?)null);

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.GetUser(OtherUserId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUser_ReturnsUnauthorized_WhenNoUserId()
    {
        Mock<IUserService> mockService = new();
        UserController controller = CreateController(mockService, userId: null);

        IActionResult result = await controller.GetUser(OtherUserId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteUser_ReturnsNoContent_WhenDeleted()
    {
        Mock<IUserService> mockService = new();

        mockService
            .Setup(s => s.DeleteAsync(OtherUserId, TestUserId))
            .ReturnsAsync(new UserDeleteResult(UserOperationStatus.Success));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.DeleteUser(OtherUserId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        Mock<IUserService> mockService = new();

        mockService
            .Setup(s => s.DeleteAsync(OtherUserId, TestUserId))
            .ReturnsAsync(new UserDeleteResult(UserOperationStatus.NotFound));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.DeleteUser(OtherUserId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteUser_ReturnsBadRequest_WhenDeletingSelf()
    {
        Mock<IUserService> mockService = new();

        mockService
            .Setup(s => s.DeleteAsync(TestUserId, TestUserId))
            .ReturnsAsync(new UserDeleteResult(UserOperationStatus.ValidationFailed));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.DeleteUser(TestUserId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteUser_ReturnsForbid_WhenNotAdmin()
    {
        Mock<IUserService> mockService = new();

        mockService
            .Setup(s => s.DeleteAsync(OtherUserId, TestUserId))
            .ReturnsAsync(new UserDeleteResult(UserOperationStatus.Forbidden));

        UserController controller = CreateController(mockService);

        IActionResult result = await controller.DeleteUser(OtherUserId);

        result.Should().BeOfType<ForbidResult>();
    }
}
