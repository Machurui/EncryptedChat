using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using EncryptedChat.Services;
using System.Security.Claims;
<<<<<<< HEAD
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
=======
>>>>>>> origin/Auth_v1.0

namespace EncryptedChat.Tests;

public class TeamControllerTests
{
    [Fact]
<<<<<<< HEAD
    public async Task PostTeam_ReturnsCreated_WhenValidAdminExists()
=======
    public async Task CreateAsync_ReturnsCreatedAtAction_WhenTeamIsCreatedSuccessfully()
>>>>>>> origin/Auth_v1.0
    {
        // Arrange
        var mockTeamService = new Mock<ITeamService>();

<<<<<<< HEAD
        var validAdminId = Guid.NewGuid().ToString();
        var teamDto = new TeamDTO
        {
            Name = "Alpha Team",
            Password = "secret",
            AdminIds = [validAdminId],
            MemberIds = []
        };

        var expectedTeam = new TeamDTOPublic
        {
            Id = 100,
            Name = "Alpha Team",
            Admins = [new UserDTOPublic { Id = validAdminId }],
            Members = []
        };

        mockTeamService
            .Setup(s => s.CreateAsync(It.Is<TeamDTO>(dto =>
                dto.AdminIds != null && dto.AdminIds.Contains(validAdminId) && dto.Name == "Alpha Team")))
            .ReturnsAsync(expectedTeam);
=======
        var fakeAdminId = Guid.NewGuid().ToString();
        var fakeMember1 = Guid.NewGuid().ToString();
        var fakeMember2 = Guid.NewGuid().ToString();

        var teamDto = new TeamDTO
        {
            Name = "Test Team",
            Password = "secret-password",
            AdminIds = [fakeAdminId],
            MemberIds = [fakeMember1, fakeMember2]
        };

        var expectedResult = new TeamDTOPublic
        {
            Id = 42,
            Name = teamDto.Name,
            Admins = teamDto.AdminIds.Select(id => new UserDTOPublic { Id = id }).ToList(),
            Members = teamDto.MemberIds.Select(id => new UserDTOPublic { Id = id }).ToList()
        };

        mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>()))
            .ReturnsAsync(expectedResult);
>>>>>>> origin/Auth_v1.0

        var controller = new TeamController(mockTeamService.Object);

        // Act
        var result = await controller.PostTeam(teamDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
<<<<<<< HEAD
    }

    [Fact]
    public async Task PostTeam_ReturnsBadRequest_WhenNoValidAdmin()
    {
        // Arrange
        var mockTeamService = new Mock<ITeamService>();

        var teamDto = new TeamDTO
        {
            Name = "No Admin Team",
            Password = "fail",
            AdminIds = [],
            MemberIds = []
        };

        // Simulate failure
        mockTeamService
            .Setup(s => s.CreateAsync(It.IsAny<TeamDTO>()))
            .ReturnsAsync((TeamDTOPublic?)null);

        var controller = new TeamController(mockTeamService.Object);

        // Act
        var result = await controller.PostTeam(teamDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Team invalid data.");
    }

    [Fact]
    public async Task GetTeams_ReturnsNotFound_WhenTheTableTeamIsEmpty()
    {
        // Arrange
        var mockTeamService = new Mock<ITeamService>();
        var controller = new TeamController(mockTeamService.Object);

        mockTeamService
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync((IEnumerable<TeamDTOPublic?>?)null);

        // Act
        var result = await controller.GetTeams();

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetTeam_ReturnsNotFound_WhenTeamDoesNotExist()
    {
        // Arrange
        var mockTeamService = new Mock<ITeamService>();
        var controller = new TeamController(mockTeamService.Object);
        var teamId = 0;

        mockTeamService
            .Setup(s => s.GetByIdAsync(teamId))
            .ReturnsAsync((TeamDTOPublic?)null);

        // Act
        var result = await controller.GetTeam(teamId);

        // Assert
        result?.Result.Should().BeOfType<NotFoundResult>();
    }

    // [Fact]
    // public async Task PutTeam_ReturnsSuccess_WhenTeamExist(){
    //     // Arrange
    //     var mockTeamService = new Mock<ITeamService>();

    //     var validAdminId = Guid.NewGuid().ToString();
    //     var teamDto = new TeamDTO
    //     {
    //         Name = "Alpha Team",
    //         Password = "secret",
    //         AdminIds = [validAdminId],
    //         MemberIds = []
    //     };

    //     var expectedTeam = new TeamDTOPublic
    //     {
    //         Id = 100,
    //         Name = "Alpha Team",
    //         Admins = [new UserDTOPublic { Id = validAdminId }],
    //         Members = []
    //     };

    //     mockTeamService
    //         .Setup(s => s.CreateAsync(It.Is<TeamDTO>(dto =>
    //             dto.AdminIds != null && dto.AdminIds.Contains(validAdminId) && dto.Name == "Alpha Team")))
    //         .ReturnsAsync(expectedTeam);

    //     var controller = new TeamController(mockTeamService.Object);

    //     // Act
    //     var result = await controller.PostTeam(teamDto);

    //     // Assert
    //     result.Should().BeOfType<CreatedAtActionResult>();
    // }
}
=======

        var createdResult = result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(TeamController.GetTeam)); // optional
        createdResult.Value.Should().BeEquivalentTo(expectedResult);
        createdResult.StatusCode.Should().Be(201);
    }
}
>>>>>>> origin/Auth_v1.0
