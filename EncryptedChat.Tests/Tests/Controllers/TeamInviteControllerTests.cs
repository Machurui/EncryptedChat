using System.Security.Claims;
using EncryptedChat.Controllers;
using EncryptedChat.Hubs;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace EncryptedChat.Tests;

public class TeamInviteControllerTests
{
    private readonly Mock<ITeamInviteService> _invites = new();
    private readonly Mock<ITeamKeyShareService> _keyShares = new();
    private readonly string _uid = Guid.NewGuid().ToString();
    private readonly Guid _teamId = Guid.NewGuid();

    private TeamController Create(string? userId)
    {
        var mockTeamService = new Mock<ITeamService>();
        var mockHubContext = new Mock<IHubContext<ChatHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var c = new TeamController(
            mockTeamService.Object,
            mockHubContext.Object,
            _keyShares.Object,
            _invites.Object);

        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        return c;
    }

    // ==================== CreateInvite ====================

    [Fact]
    public async Task CreateInvite_Admin_ReturnsOk()
    {
        _invites.Setup(s => s.CreateAsync(_teamId, _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInviteDTO("tok", DateTime.UtcNow.AddDays(7)));

        (await Create(_uid).CreateInvite(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateInvite_NonAdmin_ReturnsForbid()
    {
        _invites.Setup(s => s.CreateAsync(_teamId, _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamInviteDTO?)null);

        (await Create(_uid).CreateInvite(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateInvite_NoUser_Returns401()
    {
        (await Create(null).CreateInvite(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
    }

    // ==================== ListInvites ====================

    [Fact]
    public async Task ListInvites_Admin_ReturnsOk()
    {
        _invites.Setup(s => s.ListAsync(_teamId, _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamInviteListItemDTO>());

        (await Create(_uid).ListInvites(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ListInvites_NonAdmin_ReturnsForbid()
    {
        _invites.Setup(s => s.ListAsync(_teamId, _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TeamInviteListItemDTO>?)null);

        (await Create(_uid).ListInvites(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ListInvites_NoUser_Returns401()
    {
        (await Create(null).ListInvites(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
    }

    // ==================== RevokeInvite ====================

    [Fact]
    public async Task RevokeInvite_Admin_ReturnsNoContent()
    {
        var inviteId = Guid.NewGuid();
        _invites.Setup(s => s.RevokeAsync(_teamId, inviteId, _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        (await Create(_uid).RevokeInvite(_teamId, inviteId, CancellationToken.None))
            .Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RevokeInvite_NonAdmin_ReturnsForbid()
    {
        var inviteId = Guid.NewGuid();
        _invites.Setup(s => s.RevokeAsync(_teamId, inviteId, _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        (await Create(_uid).RevokeInvite(_teamId, inviteId, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task RevokeInvite_NoUser_Returns401()
    {
        (await Create(null).RevokeInvite(_teamId, Guid.NewGuid(), CancellationToken.None))
            .Should().BeOfType<UnauthorizedResult>();
    }

    // ==================== PreviewInvite ====================

    [Fact]
    public async Task PreviewInvite_ValidToken_ReturnsOk()
    {
        _invites.Setup(s => s.PreviewAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitePreviewDTO(_teamId, "TeamName"));

        (await Create(_uid).PreviewInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PreviewInvite_InvalidToken_ReturnsNotFound()
    {
        _invites.Setup(s => s.PreviewAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitePreviewDTO?)null);

        (await Create(_uid).PreviewInvite("bad", CancellationToken.None)).Result
            .Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PreviewInvite_NoUser_Returns401()
    {
        (await Create(null).PreviewInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
    }

    // ==================== JoinByInvite ====================

    [Fact]
    public async Task JoinByInvite_Ok_ReturnsOk()
    {
        _invites.Setup(s => s.JoinAsync("tok", _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteJoinResult(InviteJoinOutcome.Ok, new TeamDTOPublic { Id = _teamId, Name = "T" }));

        (await Create(_uid).JoinByInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task JoinByInvite_AlreadyMember_ReturnsOk()
    {
        _invites.Setup(s => s.JoinAsync("tok", _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteJoinResult(InviteJoinOutcome.AlreadyMember, new TeamDTOPublic { Id = _teamId, Name = "T" }));

        (await Create(_uid).JoinByInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task JoinByInvite_NoPublicKey_Returns409()
    {
        _invites.Setup(s => s.JoinAsync("tok", _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteJoinResult(InviteJoinOutcome.NoPublicKey, null));

        (await Create(_uid).JoinByInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task JoinByInvite_Invalid_Returns404()
    {
        _invites.Setup(s => s.JoinAsync("tok", _uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteJoinResult(InviteJoinOutcome.Invalid, null));

        (await Create(_uid).JoinByInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task JoinByInvite_NoUser_Returns401()
    {
        (await Create(null).JoinByInvite("tok", CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
    }

    // ==================== MissingKeyShare ====================

    [Fact]
    public async Task MissingKeyShare_Admin_ReturnsOk()
    {
        _keyShares.Setup(s => s.GetMembersMissingKeyShareAsync(_teamId, _uid))
            .ReturnsAsync(new List<string> { "user1" });

        (await Create(_uid).MissingKeyShare(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MissingKeyShare_NonAdmin_ReturnsForbid()
    {
        _keyShares.Setup(s => s.GetMembersMissingKeyShareAsync(_teamId, _uid))
            .ReturnsAsync((List<string>?)null);

        (await Create(_uid).MissingKeyShare(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task MissingKeyShare_NoUser_Returns401()
    {
        (await Create(null).MissingKeyShare(_teamId, CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
    }
}
