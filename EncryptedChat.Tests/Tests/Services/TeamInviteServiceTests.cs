using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EncryptedChat.Tests;

public sealed class TeamInviteServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly Mock<IUserKeysService> _userKeys = new();
    private readonly TeamInviteService _service;

    public TeamInviteServiceTests()
    {
        var options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new EncryptedChatContext(options);
        _userKeys.Setup(k => k.GetPublicKeysAsync(It.IsAny<string>()))
                 .ReturnsAsync(new PublicKeysDTO("sign", "enc"));
        _service = new TeamInviteService(_context, _userKeys.Object);
    }

    public void Dispose() => _context.Dispose();

    private async Task<Guid> SeedTeamWithAdminAsync(string adminId, string name = "Team")
    {
        var teamId = Guid.NewGuid();
        _context.Teams.Add(new Team { Id = teamId, Name = name, Slug = name.ToLowerInvariant(), KeyGeneration = 1 });
        await SeedUserAsync(adminId);
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = teamId, UserId = adminId, Role = Member.OwnerRole, UrlToken = Guid.NewGuid().ToString("N")[..16] });
        await _context.SaveChangesAsync();
        return teamId;
    }

    private async Task SeedUserAsync(string id)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == id))
        {
            _context.Users.Add(new User { Id = id, Email = $"{id}@t.com", UserName = id, Name = id, Handle = id, Level = 1 });
            await _context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_ByAdmin_ReturnsOpaqueTokenWithExpiry()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        var dto = await _service.CreateAsync(teamId, "admin", CancellationToken.None);
        dto.Should().NotBeNull();
        dto!.Token.Should().NotBeNullOrWhiteSpace();
        dto.Token.Length.Should().BeGreaterThan(20);
        dto.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        (await _context.TeamInvites.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ByNonAdmin_ReturnsNull()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        await SeedUserAsync("bob");
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = teamId, UserId = "bob", Role = Member.MemberRole, UrlToken = "x" });
        await _context.SaveChangesAsync();
        (await _service.CreateAsync(teamId, "bob", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task PreviewAsync_ValidToken_ReturnsTeamName()
    {
        var teamId = await SeedTeamWithAdminAsync("admin", "Cool Team");
        var token = (await _service.CreateAsync(teamId, "admin", CancellationToken.None))!.Token;
        var preview = await _service.PreviewAsync(token, CancellationToken.None);
        preview.Should().NotBeNull();
        preview!.TeamId.Should().Be(teamId);
        preview.TeamName.Should().Be("Cool Team");
    }

    [Fact]
    public async Task PreviewAsync_RevokedOrUnknown_ReturnsNull()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        _context.TeamInvites.Add(new TeamInvite { TeamId = teamId, Token = "tok", CreatedByUserId = "admin", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        (await _service.PreviewAsync("tok", CancellationToken.None)).Should().BeNull();
        (await _service.PreviewAsync("nope", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task PreviewAsync_Expired_ReturnsNull()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        _context.TeamInvites.Add(new TeamInvite { TeamId = teamId, Token = "old", CreatedByUserId = "admin", CreatedAt = DateTime.UtcNow.AddDays(-8), ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        await _context.SaveChangesAsync();
        (await _service.PreviewAsync("old", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task JoinAsync_ValidToken_CreatesMemberRoleMember()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        var token = (await _service.CreateAsync(teamId, "admin", CancellationToken.None))!.Token;
        await SeedUserAsync("newbie");
        var result = await _service.JoinAsync(token, "newbie", CancellationToken.None);
        result.Outcome.Should().Be(InviteJoinOutcome.Ok);
        result.Team!.Id.Should().Be(teamId);
        var m = await _context.Members.SingleAsync(x => x.TeamId == teamId && x.UserId == "newbie");
        m.Role.Should().Be(Member.MemberRole);
        m.UrlToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task JoinAsync_NoPublicKey_ReturnsNoPublicKey_AndNoMember()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        var token = (await _service.CreateAsync(teamId, "admin", CancellationToken.None))!.Token;
        await SeedUserAsync("keyless");
        _userKeys.Setup(k => k.GetPublicKeysAsync("keyless")).ReturnsAsync((PublicKeysDTO?)null);
        var result = await _service.JoinAsync(token, "keyless", CancellationToken.None);
        result.Outcome.Should().Be(InviteJoinOutcome.NoPublicKey);
        (await _context.Members.AnyAsync(x => x.UserId == "keyless")).Should().BeFalse();
    }

    [Fact]
    public async Task JoinAsync_AlreadyMember_ReturnsAlreadyMember_NoDuplicate()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        var token = (await _service.CreateAsync(teamId, "admin", CancellationToken.None))!.Token;
        var result = await _service.JoinAsync(token, "admin", CancellationToken.None);
        result.Outcome.Should().Be(InviteJoinOutcome.AlreadyMember);
        (await _context.Members.CountAsync(x => x.TeamId == teamId && x.UserId == "admin")).Should().Be(1);
    }

    [Fact]
    public async Task JoinAsync_RevokedOrExpired_ReturnsInvalid()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        _context.TeamInvites.Add(new TeamInvite { TeamId = teamId, Token = "rev", CreatedByUserId = "admin", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        await SeedUserAsync("newbie");
        (await _service.JoinAsync("rev", "newbie", CancellationToken.None)).Outcome.Should().Be(InviteJoinOutcome.Invalid);
    }

    [Fact]
    public async Task RevokeAsync_AdminThenJoinRejected()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        var created = await _service.CreateAsync(teamId, "admin", CancellationToken.None);
        var invite = await _context.TeamInvites.FirstAsync();
        await SeedUserAsync("newbie");
        (await _service.RevokeAsync(teamId, invite.Id, "admin", CancellationToken.None)).Should().BeTrue();
        (await _service.JoinAsync(created!.Token, "newbie", CancellationToken.None)).Outcome.Should().Be(InviteJoinOutcome.Invalid);
    }

    [Fact]
    public async Task RevokeAsync_ByNonAdmin_ReturnsFalse()
    {
        var teamId = await SeedTeamWithAdminAsync("admin");
        await _service.CreateAsync(teamId, "admin", CancellationToken.None);
        var invite = await _context.TeamInvites.FirstAsync();
        await SeedUserAsync("bob");
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = teamId, UserId = "bob", Role = Member.MemberRole, UrlToken = "y" });
        await _context.SaveChangesAsync();
        (await _service.RevokeAsync(teamId, invite.Id, "bob", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ForDirectTeam_ReturnsNull()
    {
        var teamId = Guid.NewGuid();
        _context.Teams.Add(new Team { Id = teamId, Name = "DM", Slug = "dm", KeyGeneration = 1, IsDirect = true });
        await SeedUserAsync("admin");
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = teamId, UserId = "admin", Role = Member.OwnerRole, UrlToken = Guid.NewGuid().ToString("N")[..16] });
        await _context.SaveChangesAsync();
        (await _service.CreateAsync(teamId, "admin", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task JoinAsync_ForDirectTeam_ReturnsInvalid()
    {
        var teamId = Guid.NewGuid();
        _context.Teams.Add(new Team { Id = teamId, Name = "DM", Slug = "dm", KeyGeneration = 1, IsDirect = true });
        await SeedUserAsync("admin");
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = teamId, UserId = "admin", Role = Member.OwnerRole, UrlToken = Guid.NewGuid().ToString("N")[..16] });
        // Manually create a valid invite row bypassing CreateAsync (which now blocks DMs):
        _context.TeamInvites.Add(new TeamInvite { TeamId = teamId, Token = "dmtok", CreatedByUserId = "admin", ExpiresAt = DateTime.UtcNow.AddDays(7) });
        await _context.SaveChangesAsync();
        await SeedUserAsync("newbie");
        (await _service.JoinAsync("dmtok", "newbie", CancellationToken.None)).Outcome.Should().Be(InviteJoinOutcome.Invalid);
    }
}
