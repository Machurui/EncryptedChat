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
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly Mock<IRateLimitService> _mockRateLimitService;
    private readonly string _userId = Guid.NewGuid().ToString();
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _messageId = Guid.NewGuid();

    public MessageControllerTests()
    {
        _mockMessageService = new Mock<IMessageService>();
        _mockTeamService = new Mock<ITeamService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
        _mockRateLimitService = new Mock<IRateLimitService>();
        _mockRateLimitService.Setup(r => r.CheckAndRecord(It.IsAny<string>()))
            .Returns(new RateLimitResult(true, 0));
    }

    private MessageController CreateController(string? userId = null)
    {
        var controller = new MessageController(_mockMessageService.Object, _mockTeamService.Object, _mockRealtimeService.Object, _mockRateLimitService.Object);
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

    private static MessageDTOPublic MakeMessageDto(Guid id, Guid teamId, string userId, string name = "User") => new()
    {
        Id = id,
        EncryptedText = "ciphertext",
        Iv = "iv",
        Signature = "sig",
        KeyGeneration = 1,
        TeamId = teamId,
        Sender = new MessageSenderDTO { Id = userId, Name = name }
    };

    private static MessageCreateDTO MakeCreateDto(Guid teamId) => new()
    {
        Team = teamId,
        EncryptedText = "ciphertext",
        Iv = "iv",
        Signature = "sig",
        KeyGeneration = 1
    };

    #region GetMessagesByTeam

    [Fact]
    public async Task GetMessagesByTeam_ReturnsOk_WhenUserIsMember()
    {
        var messages = new List<MessageDTOPublic>
        {
            MakeMessageDto(Guid.NewGuid(), _teamId, _userId),
            MakeMessageDto(Guid.NewGuid(), _teamId, _userId)
        };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.GetAllByTeamAsync(_userId, _teamId, 1, 50)).ReturnsAsync(messages);

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
            MakeMessageDto(Guid.NewGuid(), _teamId, _userId)
        };

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.GetAllByTeamAsync(_userId, _teamId, 2, 10)).ReturnsAsync(messages);

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
        _mockMessageService.Setup(s => s.GetAllByTeamAsync(_userId, _teamId, 1, 50)).ReturnsAsync((IReadOnlyList<MessageDTOPublic>?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMessagesByTeam(_teamId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetMessage

    [Fact]
    public async Task GetMessage_ReturnsOk_WhenUserIsMember()
    {
        var message = MakeMessageDto(_messageId, _teamId, _userId);

        _mockMessageService.Setup(s => s.GetByIdAsync(_messageId, _userId)).ReturnsAsync(message);

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
        _mockMessageService.Setup(s => s.GetByIdAsync(nonExistentId, _userId)).ReturnsAsync((MessageDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMessage(nonExistentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMessage_ReturnsNotFound_WhenServiceRejectsAccess()
    {
        // After True E2E v1, membership enforcement moved into the service
        // layer (GetByIdAsync filters by userId). The controller no longer
        // double-checks via ITeamService — a denied read just returns null,
        // which the controller surfaces as 404 to avoid leaking existence.
        _mockMessageService.Setup(s => s.GetByIdAsync(_messageId, _userId)).ReturnsAsync((MessageDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMessage(_messageId);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region PostMessage

    [Fact]
    public async Task PostMessage_ReturnsCreatedAtAction_WhenSuccessful()
    {
        var createDto = MakeCreateDto(_teamId);
        var createdMessage = MakeMessageDto(_messageId, _teamId, _userId);

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockTeamService.Setup(s => s.GetMemberUserIdsAsync(_teamId)).ReturnsAsync(new List<string> { _userId });
        _mockMessageService.Setup(s => s.CreateAsync(It.Is<MessageDTO>(m =>
            m.EncryptedText == "ciphertext" && m.Team == _teamId
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
        var createDto = MakeCreateDto(_teamId);

        var controller = CreateController(userId: null);
        var result = await controller.PostMessage(createDto);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PostMessage_ReturnsForbid_WhenNotMember()
    {
        var createDto = MakeCreateDto(_teamId);

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(false);

        var controller = CreateController(_userId);
        var result = await controller.PostMessage(createDto);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PostMessage_ReturnsBadRequest_WhenServiceRejectsInput()
    {
        var createDto = MakeCreateDto(_teamId);

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockMessageService.Setup(s => s.CreateAsync(It.IsAny<MessageDTO>(), _userId)).ReturnsAsync((MessageDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.PostMessage(createDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostMessage_UsesSenderFromJwt_NotFromClient()
    {
        var createDto = MakeCreateDto(_teamId);
        string? capturedSenderId = null;

        _mockTeamService.Setup(s => s.IsMemberAsync(_userId, _teamId)).ReturnsAsync(true);
        _mockTeamService.Setup(s => s.GetMemberUserIdsAsync(_teamId)).ReturnsAsync(new List<string> { _userId });
        _mockMessageService.Setup(s => s.CreateAsync(It.IsAny<MessageDTO>(), It.IsAny<string>()))
            .Callback<MessageDTO, string>((dto, senderId) => capturedSenderId = senderId)
            .ReturnsAsync(MakeMessageDto(_messageId, _teamId, _userId));

        var controller = CreateController(_userId);
        await controller.PostMessage(createDto);

        capturedSenderId.Should().Be(_userId);
    }

    #endregion

    #region DeleteMessage

    [Fact]
    public async Task DeleteMessage_ReturnsNoContent_WhenServiceSucceeds()
    {
        var message = MakeMessageDto(_messageId, _teamId, _userId);

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
