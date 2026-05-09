using System.Security.Claims;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class MessageControllerTests
{
    private readonly Mock<IMessageService> _mockMessageService;
    private readonly Mock<ITeamService> _mockTeamService;
    private readonly string _userId = Guid.NewGuid().ToString();
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _messageId = Guid.NewGuid();

    public MessageControllerTests()
    {
        _mockMessageService = new Mock<IMessageService>();
        _mockTeamService = new Mock<ITeamService>();
    }

    private MessageController CreateController(string? userId = null)
    {
        var controller = new MessageController(_mockMessageService.Object, _mockTeamService.Object);
        var claims = new List<Claim>();

        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    #region GetMessagesByTeam

    [Fact]
    public async Task GetMessagesByTeam_ReturnsOk_WhenUserIsMember()
    {
        var messages = new List<MessageDTOPublic>
        {
            new() { Id = Guid.NewGuid(), Text = "Hello", TeamId = _teamId, Sender = new MessageSenderDTO { Id = _userId, Name = "User" } },
            new() { Id = Guid.NewGuid(), Text = "World", TeamId = _teamId, Sender = new MessageSenderDTO { Id = _userId, Name = "User" } }
        };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.GetAllByTeamAsync(_teamId, 1, 50)).ReturnsAsync(messages);

        var controller = CreateController(_userId);
        var result = await controller.GetMessagesByTeam(_teamId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(messages);
    }

    [Fact]
    public async Task GetMessagesByTeam_ReturnsOk_WithPagination()
    {
        var messages = new List<MessageDTOPublic>
        {
            new() { Id = Guid.NewGuid(), Text = "Page 2", TeamId = _teamId }
        };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.GetAllByTeamAsync(_teamId, 2, 10)).ReturnsAsync(messages);

        var controller = CreateController(_userId);
        var result = await controller.GetMessagesByTeam(_teamId, page: 2, pageSize: 10);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(messages);
    }

    [Fact]
    public async Task GetMessagesByTeam_ReturnsUnauthorized_WhenNoUserId()
    {
        var controller = CreateController(userId: null);
        var result = await controller.GetMessagesByTeam(_teamId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMessagesByTeam_ReturnsForbid_WhenNotMember()
    {
        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(false);

        var controller = CreateController(_userId);
        var result = await controller.GetMessagesByTeam(_teamId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetMessagesByTeam_ReturnsNotFound_WhenTeamDoesNotExist()
    {
        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.GetAllByTeamAsync(_teamId, 1, 50)).ReturnsAsync((IReadOnlyList<MessageDTOPublic>?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMessagesByTeam(_teamId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetMessage

    [Fact]
    public async Task GetMessage_ReturnsOk_WhenUserIsMember()
    {
        var message = new MessageDTOPublic
        {
            Id = _messageId,
            Text = "Hello",
            TeamId = _teamId,
            Sender = new MessageSenderDTO { Id = _userId, Name = "User" }
        };

        _mockMessageService.Setup(s => s.GetByIdAsync(_messageId)).ReturnsAsync(message);
        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);

        var controller = CreateController(_userId);
        var result = await controller.GetMessage(_messageId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(message);
    }

    [Fact]
    public async Task GetMessage_ReturnsUnauthorized_WhenNoUserId()
    {
        var controller = CreateController(userId: null);
        var result = await controller.GetMessage(_messageId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMessage_ReturnsNotFound_WhenMessageDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();
        _mockMessageService.Setup(s => s.GetByIdAsync(nonExistentId)).ReturnsAsync((MessageDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMessage(nonExistentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMessage_ReturnsForbid_WhenNotMemberOfTeam()
    {
        var otherTeamId = Guid.NewGuid();
        var message = new MessageDTOPublic
        {
            Id = _messageId,
            Text = "Secret",
            TeamId = otherTeamId,
            Sender = new MessageSenderDTO { Id = "other-user", Name = "Other" }
        };

        _mockMessageService.Setup(s => s.GetByIdAsync(_messageId)).ReturnsAsync(message);
        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, otherTeamId)).ReturnsAsync(false);

        var controller = CreateController(_userId);
        var result = await controller.GetMessage(_messageId);

        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region PostMessage

    [Fact]
    public async Task PostMessage_ReturnsCreatedAtAction_WhenSuccessful()
    {
        var createDto = new MessageCreateDTO { Text = "Hello team!", Team = _teamId };
        var createdMessage = new MessageDTOPublic
        {
            Id = _messageId,
            Text = "Hello team!",
            TeamId = _teamId,
            Sender = new MessageSenderDTO { Id = _userId, Name = "User" },
            SignatureVerified = true
        };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.CreateAsync(It.Is<MessageDTO>(m =>
            m.Text == "Hello team!" && m.Team == _teamId
        ), _userId)).ReturnsAsync(createdMessage);

        var controller = CreateController(_userId);
        var result = await controller.PostMessage(createDto);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(MessageController.GetMessage));
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(_messageId);
        createdResult.Value.Should().BeEquivalentTo(createdMessage);
    }

    [Fact]
    public async Task PostMessage_ReturnsUnauthorized_WhenNoUserId()
    {
        var createDto = new MessageCreateDTO { Text = "Hello", Team = _teamId };

        var controller = CreateController(userId: null);
        var result = await controller.PostMessage(createDto);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PostMessage_ReturnsForbid_WhenNotMember()
    {
        var createDto = new MessageCreateDTO { Text = "Hello", Team = _teamId };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(false);

        var controller = CreateController(_userId);
        var result = await controller.PostMessage(createDto);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PostMessage_ReturnsBadRequest_WhenServiceRejectsInput()
    {
        var createDto = new MessageCreateDTO { Text = "", Team = _teamId };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.CreateAsync(It.IsAny<MessageDTO>(), _userId)).ReturnsAsync((MessageDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.PostMessage(createDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostMessage_UsesSenderFromJwt_NotFromClient()
    {
        var createDto = new MessageCreateDTO { Text = "Hello", Team = _teamId };
        string? capturedSenderId = null;

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.CreateAsync(It.IsAny<MessageDTO>(), It.IsAny<string>()))
            .Callback<MessageDTO, string>((dto, senderId) => capturedSenderId = senderId)
            .ReturnsAsync(new MessageDTOPublic { Id = _messageId, Text = "Hello", TeamId = _teamId });

        var controller = CreateController(_userId);
        await controller.PostMessage(createDto);

        capturedSenderId.Should().Be(_userId);
    }

    #endregion

    #region DeleteMessage

    [Fact]
    public async Task DeleteMessage_ReturnsNoContent_WhenServiceSucceeds()
    {
        var message = new MessageDTOPublic
        {
            Id = _messageId,
            Text = "My message",
            TeamId = _teamId,
            Sender = new MessageSenderDTO { Id = _userId, Name = "User" }
        };

        _mockMessageService.Setup(s => s.DeleteAsync(_messageId, _userId)).ReturnsAsync(message);

        var controller = CreateController(_userId);
        var result = await controller.DeleteMessage(_messageId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteMessage_ReturnsUnauthorized_WhenNoUserId()
    {
        var controller = CreateController(userId: null);
        var result = await controller.DeleteMessage(_messageId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteMessage_ReturnsNotFound_WhenServiceReturnsNull()
    {
        _mockMessageService.Setup(s => s.DeleteAsync(_messageId, _userId)).ReturnsAsync((MessageDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.DeleteMessage(_messageId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
