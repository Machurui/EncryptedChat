using System.Security.Claims;
using EncryptedChat.Hubs;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace EncryptedChat.Tests;

public class ChatHubTests
{
    private readonly Mock<IMessageService> _mockMessageService = new();
    private readonly Mock<ITeamService> _mockTeamService = new();
    private readonly Mock<IUserService> _mockUserService = new();
    private readonly Mock<IFriendService> _mockFriendService = new();
    private readonly Mock<IRealtimeService> _mockRealtime = new();
    private readonly Mock<IPresenceService> _mockPresence = new();
    private readonly Mock<IRateLimitService> _mockRateLimit = new();
    private readonly Mock<ILogger<ChatHub>> _mockLogger = new();
    private readonly Mock<IHubContext<ChatHub>> _mockHubContext = new();
    private readonly Mock<IHubClients> _mockClients = new();
    private readonly Mock<IClientProxy> _mockUserProxy = new();
    private readonly Mock<IGroupManager> _mockGroups = new();
    private readonly string _senderId = Guid.NewGuid().ToString();
    private readonly string _friendId = Guid.NewGuid().ToString();
    private readonly Guid _teamId = Guid.NewGuid();

    public ChatHubTests()
    {
        _mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(_mockUserProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockHubContext.Setup(h => h.Groups).Returns(_mockGroups.Object);

        _mockRateLimit
            .Setup(r => r.CheckAndRecord(It.IsAny<string>()))
            .Returns(new RateLimitResult(true, 0));
    }

    private ChatHub CreateHub(string userId, bool isDirect, int messageCount)
    {
        _mockTeamService.Setup(t => t.IsMemberAsync(userId, _teamId)).ReturnsAsync(true);
        _mockTeamService.Setup(t => t.GetMemberUserIdsAsync(_teamId))
            .ReturnsAsync(new List<string> { userId, _friendId });

        TeamDTOPublic teamDto = new()
        {
            Id = _teamId,
            Name = "Conversation",
            Slug = "conv-slug",
            IsDirect = isDirect,
        };
        _mockTeamService.Setup(t => t.GetByIdAsync(_teamId)).ReturnsAsync(teamDto);

        _mockMessageService.Setup(m => m.CountByTeamAsync(_teamId)).ReturnsAsync(messageCount);

        MessageDTOPublic savedMessage = new()
        {
            Id = Guid.NewGuid(),
            EncryptedText = "ciphertext",
            Iv = "iv",
            Signature = "sig",
            KeyGeneration = 1,
            TeamId = _teamId,
            Date = DateTime.UtcNow,
            Sender = new MessageSenderDTO { Id = userId, Name = "Sender" }
        };
        _mockMessageService.Setup(m => m.CreateAsync(It.IsAny<MessageDTO>(), userId))
            .ReturnsAsync(savedMessage);

        ChatHub hub = new(
            _mockMessageService.Object,
            _mockTeamService.Object,
            _mockUserService.Object,
            _mockFriendService.Object,
            _mockRealtime.Object,
            _mockPresence.Object,
            _mockRateLimit.Object,
            _mockLogger.Object,
            _mockHubContext.Object);

        Mock<HubCallerContext> mockCallerContext = new();
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, userId)];
        mockCallerContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
        mockCallerContext.Setup(c => c.ConnectionId).Returns(Guid.NewGuid().ToString());
        hub.Context = mockCallerContext.Object;

        return hub;
    }

    [Fact(Skip = "Server-side crypto removed in True E2E v1; ChatHub signature in flux. Tests rewrite in a later phase.")]
    public async Task SendMessageToTeam_FirstDmMessage_NotifiesFriendDirectly()
    {
        ChatHub hub = CreateHub(_senderId, isDirect: true, messageCount: 1);

        await hub.SendMessageToTeam(_teamId, "hello", "iv", "sig", 1);

        _mockUserProxy.Verify(p => p.SendCoreAsync(
            "DirectMessageCreated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUserProxy.Verify(p => p.SendCoreAsync(
            "ReceiveMessage",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRealtime.Verify(r => r.BroadcastMessageAsync(_teamId, It.IsAny<MessageDTOPublic>()), Times.Once);
    }

    [Fact(Skip = "Server-side crypto removed in True E2E v1; ChatHub signature in flux. Tests rewrite in a later phase.")]
    public async Task SendMessageToTeam_SecondDmMessage_DoesNotNotifyFriendDirectly()
    {
        ChatHub hub = CreateHub(_senderId, isDirect: true, messageCount: 2);

        await hub.SendMessageToTeam(_teamId, "second", "iv", "sig", 1);

        _mockUserProxy.Verify(p => p.SendCoreAsync(
            "DirectMessageCreated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _mockUserProxy.Verify(p => p.SendCoreAsync(
            "ReceiveMessage",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _mockRealtime.Verify(r => r.BroadcastMessageAsync(_teamId, It.IsAny<MessageDTOPublic>()), Times.Once);
    }

    [Fact(Skip = "Server-side crypto removed in True E2E v1; ChatHub signature in flux. Tests rewrite in a later phase.")]
    public async Task SendMessageToTeam_NonDmFirstMessage_DoesNotNotifyAnyoneDirectly()
    {
        ChatHub hub = CreateHub(_senderId, isDirect: false, messageCount: 1);

        await hub.SendMessageToTeam(_teamId, "team-hello", "iv", "sig", 1);

        _mockUserProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _mockRealtime.Verify(r => r.BroadcastMessageAsync(_teamId, It.IsAny<MessageDTOPublic>()), Times.Once);
    }
}
