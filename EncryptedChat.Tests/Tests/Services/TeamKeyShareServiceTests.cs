using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Tests;

public sealed class TeamKeyShareServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly TeamKeyShareService _service;

    public TeamKeyShareServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new EncryptedChatContext(options);
        _service = new TeamKeyShareService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetMineForTeamAsync_ReturnsOnlyCallerOwnShares()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "bob", "Member");
        await SeedKeyShareAsync(teamId, "alice", 1, "aliceWrappedGen1");
        await SeedKeyShareAsync(teamId, "bob", 1, "bobWrappedGen1");

        var aliceView = await _service.GetMineForTeamAsync("alice", teamId);

        aliceView.Should().HaveCount(1);
        aliceView[0].WrappedKey.Should().Be("aliceWrappedGen1");
    }

    [Fact]
    public async Task GetMineForTeamAsync_ReturnsEmpty_WhenCallerNotMember()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");

        var view = await _service.GetMineForTeamAsync("eve", teamId);

        view.Should().BeEmpty();
    }

    [Fact]
    public async Task InsertKeyShareForMemberAsync_RejectsNonAdmin()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "bob", "Member");
        await SeedMemberAsync(teamId, "carol", "Member");

        var result = await _service.InsertKeyShareForMemberAsync("bob", teamId, "carol", "wrapped");

        result.Should().Be(KeyShareInsertResult.Forbidden);
    }

    [Fact]
    public async Task InsertKeyShareForMemberAsync_PersistsAtCurrentGeneration()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 2);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "newbie", "Member");

        var result = await _service.InsertKeyShareForMemberAsync("alice", teamId, "newbie", "wrappedG2");

        result.Should().Be(KeyShareInsertResult.Ok);
        var share = await _context.TeamKeyShares.SingleAsync(k => k.MemberId == "newbie");
        share.Generation.Should().Be(2);
        share.WrappedKey.Should().Be("wrappedG2");
    }

    [Fact]
    public async Task InsertKeyShareForMemberAsync_ReturnsAlreadyExists_OnSecondInsert()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "newbie", "Member");

        var first = await _service.InsertKeyShareForMemberAsync("alice", teamId, "newbie", "wrapped");
        var second = await _service.InsertKeyShareForMemberAsync("alice", teamId, "newbie", "wrapped");

        first.Should().Be(KeyShareInsertResult.Ok);
        second.Should().Be(KeyShareInsertResult.AlreadyExists);
    }

    [Fact]
    public async Task RemoveMemberAndRotateAsync_IncrementsGenerationAndDeletesRemovedShares()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "bob", "Member");
        await SeedMemberAsync(teamId, "carol", "Member");
        await SeedKeyShareAsync(teamId, "alice", 1, "aliceG1");
        await SeedKeyShareAsync(teamId, "bob", 1, "bobG1");
        await SeedKeyShareAsync(teamId, "carol", 1, "carolG1");

        var result = await _service.RemoveMemberAndRotateAsync("alice", teamId, "bob", new[]
        {
            new KeyShareEntryDTO("alice", "aliceG2"),
            new KeyShareEntryDTO("carol", "carolG2")
        });

        result.Should().Be(RemoveAndRotateResult.Ok);

        var team = await _context.Teams.AsNoTracking().FirstAsync(t => t.Id == teamId);
        team.KeyGeneration.Should().Be(2);

        var bobShares = await _context.TeamKeyShares.Where(k => k.MemberId == "bob").ToListAsync();
        bobShares.Should().BeEmpty();

        var aliceShares = await _context.TeamKeyShares.Where(k => k.MemberId == "alice").OrderBy(k => k.Generation).ToListAsync();
        aliceShares.Should().HaveCount(2);
        aliceShares[0].Generation.Should().Be(1);
        aliceShares[1].Generation.Should().Be(2);
    }

    [Fact]
    public async Task RemoveMemberAndRotateAsync_RejectsCoverageMismatch()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "bob", "Member");
        await SeedMemberAsync(teamId, "carol", "Member");

        // Missing carol's share — should be rejected
        var result = await _service.RemoveMemberAndRotateAsync("alice", teamId, "bob", new[]
        {
            new KeyShareEntryDTO("alice", "aliceG2")
        });

        result.Should().Be(RemoveAndRotateResult.KeyShareCoverageMismatch);
    }

    [Fact]
    public async Task RemoveMemberAndRotateAsync_CannotRemoveLastAdmin()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamAsync(teamId, "Team", generation: 1);
        await SeedMemberAsync(teamId, "alice", "Admin");
        await SeedMemberAsync(teamId, "bob", "Member");

        var result = await _service.RemoveMemberAndRotateAsync("alice", teamId, "alice", new[]
        {
            new KeyShareEntryDTO("bob", "bobG2")
        });

        result.Should().Be(RemoveAndRotateResult.CannotRemoveLastAdmin);
    }

    private async Task SeedTeamAsync(Guid id, string name, int generation)
    {
        _context.Teams.Add(new Team
        {
            Id = id,
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            KeyGeneration = generation
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedMemberAsync(Guid teamId, string userId, string role)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == userId))
        {
            _context.Users.Add(new User
            {
                Id = userId,
                Email = $"{userId}@test.com",
                UserName = userId,
                Name = userId,
                Handle = userId,
                Level = 1
            });
        }

        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = role,
            UrlToken = Guid.NewGuid().ToString("N")[..16]
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedKeyShareAsync(Guid teamId, string memberId, int generation, string wrappedKey)
    {
        _context.TeamKeyShares.Add(new TeamKeyShare
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MemberId = memberId,
            Generation = generation,
            WrappedKey = wrappedKey,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }
}
