using System.Security.Claims;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class TeamControllerTests
{
    private readonly Mock<ITeamService> _mockTeamService;
    private readonly string _userId = Guid.NewGuid().ToString();

    public TeamControllerTests()
    {
        _mockTeamService = new Mock<ITeamService>();
    }

    private TeamController CreateController(string? userId = null)
    {
        TeamController controller = new(_mockTeamService.Object);
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
    public async Task PostTeam_ReturnsCreatedAtAction_WhenTeamIsCreatedSuccessfully()
    {
        Guid teamId = Guid.NewGuid();
        TeamDTO teamDto = new()
        {
            Name = "Test Team",
            Admins = [_userId],
            Members = []
        };
        TeamDTOPublic expectedTeam = new()
        {
            Id = teamId,
            Name = teamDto.Name,
            Slug = "test-team",
            Members =
            [
                new MemberDTOPublic
                {
                    User = new UserDTOPublic { Id = _userId },
                    Role = Member.AdminRole
                }
            ]
        };

        _mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>(), _userId))
            .ReturnsAsync(expectedTeam);

        TeamController controller = CreateController(_userId);
        IActionResult? result = await controller.PostTeam(teamDto);

        CreatedAtActionResult createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TeamController.GetTeam));
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(teamId);
        createdResult.Value.Should().BeEquivalentTo(expectedTeam);
    }

    [Fact]
    public async Task PostTeam_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamDTO teamDto = new() { Name = "Test", Admins = ["someone"] };

        TeamController controller = CreateController(userId: null);
        IActionResult? result = await controller.PostTeam(teamDto);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PostTeam_ReturnsBadRequest_WhenServiceRejectsInput()
    {
        TeamDTO teamDto = new()
        {
            Name = "No Admin Team",
            Admins = [],
            Members = []
        };

        _mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>(), _userId))
            .ReturnsAsync((TeamDTOPublic?)null);

        TeamController controller = CreateController(_userId);
        IActionResult? result = await controller.PostTeam(teamDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostTeam_CreatorIsAlwaysAdmin()
    {
        Guid teamId = Guid.NewGuid();
        string otherAdminId = Guid.NewGuid().ToString();
        TeamDTO teamDto = new()
        {
            Name = "Test Team",
            Admins = [otherAdminId],
            Members = []
        };

        _mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>(), _userId))
            .ReturnsAsync(new TeamDTOPublic { Id = teamId, Name = "Test Team", Slug = "test-team" });

        TeamController controller = CreateController(_userId);
        await controller.PostTeam(teamDto);

        _mockTeamService.Verify(s => s.CreateAsync(It.IsAny<TeamDTO>(), _userId), Times.Once);
    }

    [Fact]
    public async Task GetTeam_ReturnsNotFound_WhenTeamDoesNotExist()
    {
        Guid teamId = Guid.NewGuid();

        _mockTeamService
            .Setup(s => s.GetByIdAsync(teamId))
            .ReturnsAsync((TeamDTOPublic?)null);

        TeamController controller = CreateController(_userId);
        ActionResult<TeamDTOPublic?>? result = await controller.GetTeam(teamId);

        result?.Result.Should().BeOfType<NotFoundResult>();
    }
}
