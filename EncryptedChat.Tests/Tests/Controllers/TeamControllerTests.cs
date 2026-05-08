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

    #region PatchTeam

    [Fact]
    public async Task PatchTeam_ReturnsNoContent_WhenSuccessful()
    {
        Guid teamId = Guid.NewGuid();
        TeamUpdateDTO dto = new() { Name = "New Name" };

        _mockTeamService
            .Setup(s => s.UpdateNameAsync(teamId, "New Name", _userId))
            .ReturnsAsync(new TeamDTOPublic { Id = teamId, Name = "New Name", Slug = "new-name" });

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.PatchTeam(teamId, dto);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PatchTeam_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamController controller = CreateController(userId: null);
        IActionResult result = await controller.PatchTeam(Guid.NewGuid(), new TeamUpdateDTO { Name = "Name" });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PatchTeam_ReturnsNotFound_WhenServiceReturnsNull()
    {
        Guid teamId = Guid.NewGuid();

        _mockTeamService
            .Setup(s => s.UpdateNameAsync(teamId, It.IsAny<string>(), _userId))
            .ReturnsAsync((TeamDTOPublic?)null);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.PatchTeam(teamId, new TeamUpdateDTO { Name = "Name" });

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region DeleteTeam

    [Fact]
    public async Task DeleteTeam_ReturnsNoContent_WhenSuccessful()
    {
        Guid teamId = Guid.NewGuid();

        _mockTeamService
            .Setup(s => s.DeleteAsync(teamId, _userId))
            .ReturnsAsync(new TeamDTOPublic { Id = teamId, Name = "Deleted", Slug = "deleted" });

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.DeleteTeam(teamId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteTeam_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamController controller = CreateController(userId: null);
        IActionResult result = await controller.DeleteTeam(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteTeam_ReturnsNotFound_WhenServiceReturnsNull()
    {
        Guid teamId = Guid.NewGuid();

        _mockTeamService
            .Setup(s => s.DeleteAsync(teamId, _userId))
            .ReturnsAsync((TeamDTOPublic?)null);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.DeleteTeam(teamId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region AddMember

    [Fact]
    public async Task AddMember_ReturnsNoContent_WhenSuccessful()
    {
        Guid teamId = Guid.NewGuid();
        string newMemberId = Guid.NewGuid().ToString();

        _mockTeamService
            .Setup(s => s.AddMemberAsync(teamId, newMemberId, _userId))
            .ReturnsAsync(true);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.AddMember(teamId, new MemberActionDTO(newMemberId));

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AddMember_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamController controller = CreateController(userId: null);
        IActionResult result = await controller.AddMember(Guid.NewGuid(), new MemberActionDTO("user"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AddMember_ReturnsNotFound_WhenServiceReturnsFalse()
    {
        Guid teamId = Guid.NewGuid();

        _mockTeamService
            .Setup(s => s.AddMemberAsync(teamId, It.IsAny<string>(), _userId))
            .ReturnsAsync(false);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.AddMember(teamId, new MemberActionDTO("user"));

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region RemoveMember

    [Fact]
    public async Task RemoveMember_ReturnsNoContent_WhenSuccessful()
    {
        Guid teamId = Guid.NewGuid();
        string memberId = Guid.NewGuid().ToString();

        _mockTeamService
            .Setup(s => s.RemoveMemberAsync(teamId, memberId, _userId))
            .ReturnsAsync(true);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.RemoveMember(teamId, memberId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveMember_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamController controller = CreateController(userId: null);
        IActionResult result = await controller.RemoveMember(Guid.NewGuid(), "user");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RemoveMember_ReturnsBadRequest_WhenRemovingSelf()
    {
        Guid teamId = Guid.NewGuid();

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.RemoveMember(teamId, _userId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveMember_ReturnsNotFound_WhenServiceReturnsFalse()
    {
        Guid teamId = Guid.NewGuid();
        string memberId = Guid.NewGuid().ToString();

        _mockTeamService
            .Setup(s => s.RemoveMemberAsync(teamId, memberId, _userId))
            .ReturnsAsync(false);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.RemoveMember(teamId, memberId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region PromoteToAdmin

    [Fact]
    public async Task PromoteToAdmin_ReturnsNoContent_WhenSuccessful()
    {
        Guid teamId = Guid.NewGuid();
        string memberId = Guid.NewGuid().ToString();

        _mockTeamService
            .Setup(s => s.PromoteToAdminAsync(teamId, memberId, _userId))
            .ReturnsAsync(true);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.PromoteToAdmin(teamId, new MemberActionDTO(memberId));

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PromoteToAdmin_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamController controller = CreateController(userId: null);
        IActionResult result = await controller.PromoteToAdmin(Guid.NewGuid(), new MemberActionDTO("user"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PromoteToAdmin_ReturnsNotFound_WhenServiceReturnsFalse()
    {
        Guid teamId = Guid.NewGuid();

        _mockTeamService
            .Setup(s => s.PromoteToAdminAsync(teamId, It.IsAny<string>(), _userId))
            .ReturnsAsync(false);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.PromoteToAdmin(teamId, new MemberActionDTO("user"));

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region DemoteFromAdmin

    [Fact]
    public async Task DemoteFromAdmin_ReturnsNoContent_WhenSuccessful()
    {
        Guid teamId = Guid.NewGuid();
        string adminId = Guid.NewGuid().ToString();

        _mockTeamService
            .Setup(s => s.DemoteFromAdminAsync(teamId, adminId, _userId))
            .ReturnsAsync(true);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.DemoteFromAdmin(teamId, adminId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DemoteFromAdmin_ReturnsUnauthorized_WhenNoUserId()
    {
        TeamController controller = CreateController(userId: null);
        IActionResult result = await controller.DemoteFromAdmin(Guid.NewGuid(), "user");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DemoteFromAdmin_ReturnsBadRequest_WhenDemotingSelf()
    {
        Guid teamId = Guid.NewGuid();

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.DemoteFromAdmin(teamId, _userId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DemoteFromAdmin_ReturnsNotFound_WhenServiceReturnsFalse()
    {
        Guid teamId = Guid.NewGuid();
        string adminId = Guid.NewGuid().ToString();

        _mockTeamService
            .Setup(s => s.DemoteFromAdminAsync(teamId, adminId, _userId))
            .ReturnsAsync(false);

        TeamController controller = CreateController(_userId);
        IActionResult result = await controller.DemoteFromAdmin(teamId, adminId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
