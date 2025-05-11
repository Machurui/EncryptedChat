using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using EncryptedChat.Services;
using System.Security.Claims;

namespace EncryptedChat.Tests;

public class TeamControllerTests
{
    [Fact]
    public async Task CreateAsync_ReturnsCreatedAtAction_WhenTeamIsCreatedSuccessfully()
    {
        // Arrange
        var mockTeamService = new Mock<ITeamService>();

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

        var controller = new TeamController(mockTeamService.Object);

        // Act
        var result = await controller.PostTeam(teamDto);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();

        var createdResult = result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(TeamController.GetTeam)); // optional
        createdResult.Value.Should().BeEquivalentTo(expectedResult);
        createdResult.StatusCode.Should().Be(201);
    }
}
