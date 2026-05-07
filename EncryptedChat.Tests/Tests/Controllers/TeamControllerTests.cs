using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class TeamControllerTests
{
    [Fact]
    public async Task PostTeam_ReturnsCreatedAtAction_WhenTeamIsCreatedSuccessfully()
    {
        var mockTeamService = new Mock<ITeamService>();
        var adminId = Guid.NewGuid().ToString();
        var memberId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid();
        var teamDto = new TeamDTO
        {
            Name = "Test Team",
            Admins = [adminId],
            Members = [memberId]
        };
        var expectedTeam = new TeamDTOPublic
        {
            Id = teamId,
            Name = teamDto.Name,
            Slug = "test-team",
            Members =
            [
                new MemberDTOPublic
                {
                    Id = Guid.NewGuid(),
                    User = new UserDTOPublic { Id = adminId },
                    Role = Member.AdminRole
                },
                new MemberDTOPublic
                {
                    Id = Guid.NewGuid(),
                    User = new UserDTOPublic { Id = memberId },
                    Role = Member.MemberRole
                }
            ],
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>()))
            .ReturnsAsync(expectedTeam);

        var controller = new TeamController(mockTeamService.Object);

        var result = await controller.PostTeam(teamDto);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TeamController.GetTeam));
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(teamId);
        createdResult.Value.Should().BeEquivalentTo(expectedTeam);
    }

    [Fact]
    public async Task PostTeam_ReturnsBadRequest_WhenServiceRejectsInput()
    {
        var mockTeamService = new Mock<ITeamService>();
        var teamDto = new TeamDTO
        {
            Name = "No Admin Team",
            Admins = [],
            Members = []
        };

        mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>()))
            .ReturnsAsync((TeamDTOPublic?)null);

        var controller = new TeamController(mockTeamService.Object);

        var result = await controller.PostTeam(teamDto);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Team invalid data.");
    }

    [Fact]
    public async Task GetTeam_ReturnsNotFound_WhenTeamDoesNotExist()
    {
        var mockTeamService = new Mock<ITeamService>();
        var teamId = Guid.NewGuid();

        mockTeamService
            .Setup(s => s.GetByIdAsync(teamId))
            .ReturnsAsync((TeamDTOPublic?)null);

        var controller = new TeamController(mockTeamService.Object);

        var result = await controller.GetTeam(teamId);

        result?.Result.Should().BeOfType<NotFoundResult>();
    }
}
