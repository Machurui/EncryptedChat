using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EncryptedChat.Tests;

public class FriendServiceTests
{
    private readonly EncryptedChatContext _context;
    private readonly FriendService _service;
    private readonly Mock<IPresenceService> _presenceServiceMock;

    public FriendServiceTests()
    {
        var options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new EncryptedChatContext(options);
        _presenceServiceMock = new Mock<IPresenceService>();
        _presenceServiceMock.Setup(p => p.IsOnline(It.IsAny<string>())).Returns(false);
        _service = new FriendService(_context, _presenceServiceMock.Object);
    }

    private async Task<(string userId, string friendId)> SetupAcceptedFriendshipAsync()
    {
        // TEMP-Task3: var user = new User { Id = Guid.NewGuid().ToString(), UserName = "alice", Name = "Alice", NameColor = "#FFFFFF", Secret = "secret-a" };
        // TEMP-Task3: var friend = new User { Id = Guid.NewGuid().ToString(), UserName = "bob", Name = "Bob", NameColor = "#FFFFFF", Secret = "secret-b" };
        var user = new User { Id = Guid.NewGuid().ToString(), UserName = "alice", Name = "Alice", NameColor = "#FFFFFF" };
        var friend = new User { Id = Guid.NewGuid().ToString(), UserName = "bob", Name = "Bob", NameColor = "#FFFFFF" };
        _context.Users.AddRange(user, friend);

        _context.Friendships.Add(new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = user.Id,
            AddresseeId = friend.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();
        return (user.Id, friend.Id);
    }

    private async Task<Guid> AddDmAsync(string userIdA, string userIdB)
    {
        var dm = new Team
        {
            Id = Guid.NewGuid(),
            Name = "DM",
            Slug = $"dm-{Guid.NewGuid():N}",
            Glyph = "◆",
            Color = "oklch(0.65 0.16 165)",
            // TEMP-Task3: Secret = "dm-secret",
            IsDirect = true,
        };
        _context.Teams.Add(dm);
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = dm.Id, UserId = userIdA, Role = Member.MemberRole, UrlToken = Guid.NewGuid().ToString("N")[..10] });
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = dm.Id, UserId = userIdB, Role = Member.MemberRole, UrlToken = Guid.NewGuid().ToString("N")[..10] });
        await _context.SaveChangesAsync();
        return dm.Id;
    }

    [Fact]
    public async Task RemoveFriendAsync_ReturnsDmId_WhenDmExists()
    {
        var (userId, friendId) = await SetupAcceptedFriendshipAsync();
        var dmId = await AddDmAsync(userId, friendId);

        var (success, removedFriendId, deletedDmId) = await _service.RemoveFriendAsync(userId, friendId);

        success.Should().BeTrue();
        removedFriendId.Should().Be(friendId);
        deletedDmId.Should().Be(dmId);

        (await _context.Teams.AnyAsync(t => t.Id == dmId)).Should().BeFalse();
        (await _context.Members.AnyAsync(m => m.TeamId == dmId)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveFriendAsync_ReturnsNullDmId_WhenNoDmExists()
    {
        var (userId, friendId) = await SetupAcceptedFriendshipAsync();

        var (success, removedFriendId, deletedDmId) = await _service.RemoveFriendAsync(userId, friendId);

        success.Should().BeTrue();
        removedFriendId.Should().Be(friendId);
        deletedDmId.Should().BeNull();
    }
}
