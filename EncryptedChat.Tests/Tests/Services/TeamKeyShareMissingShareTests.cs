using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Tests;

public sealed class TeamKeyShareMissingShareTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly TeamKeyShareService _service;

    public TeamKeyShareMissingShareTests()
    {
        var options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new EncryptedChatContext(options);
        _service = new TeamKeyShareService(_context);
    }
    public void Dispose() => _context.Dispose();

    private void AddTeam(Guid teamId, int gen) => _context.Teams.Add(new Team { Id = teamId, Name = "T", Slug = "t", KeyGeneration = gen });
    private void AddMember(Guid teamId, string uid, string role) => _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = teamId, UserId = uid, Role = role, UrlToken = Guid.NewGuid().ToString("N")[..16] });
    private void AddShare(Guid teamId, string uid, int gen) => _context.TeamKeyShares.Add(new TeamKeyShare { Id = Guid.NewGuid(), TeamId = teamId, MemberId = uid, Generation = gen, WrappedKey = "w", CreatedAt = DateTime.UtcNow });

    [Fact]
    public async Task ReturnsMembersWithoutShareAtCurrentGen()
    {
        var teamId = Guid.NewGuid();
        AddTeam(teamId, 2);
        AddMember(teamId, "admin", Member.OwnerRole);
        AddMember(teamId, "alice", Member.MemberRole);
        AddMember(teamId, "newbie", Member.MemberRole);
        AddShare(teamId, "admin", 2);
        AddShare(teamId, "alice", 2);
        AddShare(teamId, "newbie", 1); // stale gen only
        await _context.SaveChangesAsync();

        var missing = await _service.GetMembersMissingKeyShareAsync(teamId, "admin");

        missing.Should().BeEquivalentTo(new[] { "newbie" });
    }

    [Fact]
    public async Task ReturnsNull_WhenActorNotAdmin()
    {
        var teamId = Guid.NewGuid();
        AddTeam(teamId, 1);
        AddMember(teamId, "bob", Member.MemberRole);
        await _context.SaveChangesAsync();
        (await _service.GetMembersMissingKeyShareAsync(teamId, "bob")).Should().BeNull();
    }
}
