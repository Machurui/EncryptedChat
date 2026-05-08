using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Tests;

public class MessageServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly CryptoService _crypto;
    private readonly MessageService _service;

    public MessageServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EncryptedChatContext(options);
        _crypto = new CryptoService();
        _service = new MessageService(_context, _crypto);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<User> CreateUser(string id, string name = "TestUser")
    {
        User user = new()
        {
            Id = id,
            Name = name,
            Email = $"{id}@test.com",
            NormalizedEmail = $"{id}@TEST.COM",
            UserName = $"{id}@test.com",
            NormalizedUserName = $"{id}@TEST.COM",
            Secret = Guid.NewGuid().ToString("N")
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> CreateTeam(string name = "TestTeam")
    {
        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(" ", "-"),
            Secret = Guid.NewGuid().ToString("N")
        };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task AddMember(User user, Team team, string role = Member.MemberRole)
    {
        Member member = new()
        {
            Id = Guid.NewGuid(),
            User = user,
            UserId = user.Id,
            Team = team,
            TeamId = team.Id,
            Role = role
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
    }

    private async Task<Message> CreateMessage(User sender, Team team, string text)
    {
        (string encrypted, string iv) = _crypto.Encrypt(text, team.Secret);
        string signature = _crypto.Sign(text, sender.Secret);

        Message message = new()
        {
            EncryptedText = encrypted,
            Iv = iv,
            Signature = signature,
            Sender = sender,
            Team = team,
            Date = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    #region GetAllByTeamAsync

    [Fact]
    public async Task GetAllByTeamAsync_ReturnsMessages_DecryptedAndVerified()
    {
        User user = await CreateUser("user-1", "Alice");
        Team team = await CreateTeam("Team1");
        await AddMember(user, team);
        await CreateMessage(user, team, "Hello team!");

        IReadOnlyList<MessageDTOPublic>? result = await _service.GetAllByTeamAsync(team.Id);

        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result![0].Text.Should().Be("Hello team!");
        result[0].SignatureVerified.Should().BeTrue();
        result[0].Sender!.Id.Should().Be(user.Id);
        result[0].Sender!.Name.Should().Be("Alice");
        result[0].TeamId.Should().Be(team.Id);
    }

    [Fact]
    public async Task GetAllByTeamAsync_ReturnsNull_WhenTeamNotFound()
    {
        IReadOnlyList<MessageDTOPublic>? result = await _service.GetAllByTeamAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllByTeamAsync_ReturnsEmptyList_WhenNoMessages()
    {
        Team team = await CreateTeam("EmptyTeam");

        IReadOnlyList<MessageDTOPublic>? result = await _service.GetAllByTeamAsync(team.Id);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllByTeamAsync_OrdersByDateDescending()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        Message msg1 = await CreateMessage(user, team, "First");
        await Task.Delay(10);
        Message msg2 = await CreateMessage(user, team, "Second");

        IReadOnlyList<MessageDTOPublic>? result = await _service.GetAllByTeamAsync(team.Id);

        result.Should().HaveCount(2);
        result![0].Text.Should().Be("Second");
        result[1].Text.Should().Be("First");
    }

    [Fact]
    public async Task GetAllByTeamAsync_RespectsPagination()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        for (int i = 1; i <= 5; i++)
        {
            await CreateMessage(user, team, $"Message {i}");
            await Task.Delay(10);
        }

        IReadOnlyList<MessageDTOPublic>? page1 = await _service.GetAllByTeamAsync(team.Id, page: 1, pageSize: 2);
        IReadOnlyList<MessageDTOPublic>? page2 = await _service.GetAllByTeamAsync(team.Id, page: 2, pageSize: 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1![0].Text.Should().NotBe(page2![0].Text);
    }

    [Fact]
    public async Task GetAllByTeamAsync_ClampsPageSizeToMax()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        await CreateMessage(user, team, "Test");

        IReadOnlyList<MessageDTOPublic>? result = await _service.GetAllByTeamAsync(team.Id, page: 1, pageSize: 1000);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllByTeamAsync_NormalizesInvalidPage()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        await CreateMessage(user, team, "Test");

        IReadOnlyList<MessageDTOPublic>? result = await _service.GetAllByTeamAsync(team.Id, page: 0, pageSize: 50);

        result.Should().NotBeNull();
        result.Should().ContainSingle();
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ReturnsMessage_DecryptedAndVerified()
    {
        User user = await CreateUser("user-1", "Bob");
        Team team = await CreateTeam();
        await AddMember(user, team);
        Message message = await CreateMessage(user, team, "Hello world");

        MessageDTOPublic? result = await _service.GetByIdAsync(message.Id);

        result.Should().NotBeNull();
        result!.Text.Should().Be("Hello world");
        result.SignatureVerified.Should().BeTrue();
        result.Sender!.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        MessageDTOPublic? result = await _service.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsFalseSignature_WhenTampered()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        (string encrypted, string iv) = _crypto.Encrypt("Original", team.Secret);
        string wrongSignature = _crypto.Sign("Different text", user.Secret);

        Message message = new()
        {
            EncryptedText = encrypted,
            Iv = iv,
            Signature = wrongSignature,
            Sender = user,
            Team = team,
            Date = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        MessageDTOPublic? result = await _service.GetByIdAsync(message.Id);

        result.Should().NotBeNull();
        result!.Text.Should().Be("Original");
        result.SignatureVerified.Should().BeFalse();
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_CreatesMessage_WithEncryptionAndSignature()
    {
        User user = await CreateUser("user-1", "Creator");
        Team team = await CreateTeam();
        await AddMember(user, team);

        MessageDTO dto = new()
        {
            Text = "New message",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().NotBeNull();
        result!.Text.Should().Be("New message");
        result.SignatureVerified.Should().BeTrue();
        result.Sender!.Id.Should().Be(user.Id);
        result.TeamId.Should().Be(team.Id);

        Message? stored = await _context.Messages.FirstOrDefaultAsync(m => m.Id == result.Id);
        stored.Should().NotBeNull();
        stored!.EncryptedText.Should().NotBe("New message");
        stored.Iv.Should().NotBeNullOrEmpty();
        stored.Signature.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenSenderNotFound()
    {
        Team team = await CreateTeam();

        MessageDTO dto = new()
        {
            Text = "Hello",
            Sender = "nonexistent-user",
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenTeamNotFound()
    {
        User user = await CreateUser("user-1");

        MessageDTO dto = new()
        {
            Text = "Hello",
            Sender = user.Id,
            Team = Guid.NewGuid()
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenSenderNotMember()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();

        MessageDTO dto = new()
        {
            Text = "Hello",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenTextIsEmpty()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        MessageDTO dto = new()
        {
            Text = "",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenTextIsWhitespace()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        MessageDTO dto = new()
        {
            Text = "   ",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_HandlesUnicodeText()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        MessageDTO dto = new()
        {
            Text = "Hello 世界 🌍 Привет",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().NotBeNull();
        result!.Text.Should().Be("Hello 世界 🌍 Привет");
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenTeamIdIsNull()
    {
        User user = await CreateUser("user-1");

        MessageDTO dto = new()
        {
            Text = "Hello",
            Sender = user.Id,
            Team = null
        };

        MessageDTOPublic? result = await _service.CreateAsync(dto);

        result.Should().BeNull();
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_UpdatesMessage_ReEncrypts()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        Message message = await CreateMessage(user, team, "Original");

        string originalEncrypted = message.EncryptedText;

        MessageDTO dto = new()
        {
            Text = "Updated text",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.UpdateAsync(message.Id, dto);

        result.Should().NotBeNull();
        result!.Text.Should().Be("Updated text");

        Message? updated = await _context.Messages.FirstOrDefaultAsync(m => m.Id == message.Id);
        updated!.EncryptedText.Should().NotBe(originalEncrypted);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMessageNotFound()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();

        MessageDTO dto = new()
        {
            Text = "Updated",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.UpdateAsync(999, dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenSenderNotFound()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        Message message = await CreateMessage(user, team, "Original");

        MessageDTO dto = new()
        {
            Text = "Updated",
            Sender = "nonexistent",
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.UpdateAsync(message.Id, dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenTeamNotFound()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        Message message = await CreateMessage(user, team, "Original");

        MessageDTO dto = new()
        {
            Text = "Updated",
            Sender = user.Id,
            Team = Guid.NewGuid()
        };

        MessageDTOPublic? result = await _service.UpdateAsync(message.Id, dto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenTextIsEmpty()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        Message message = await CreateMessage(user, team, "Original");

        MessageDTO dto = new()
        {
            Text = "",
            Sender = user.Id,
            Team = team.Id
        };

        MessageDTOPublic? result = await _service.UpdateAsync(message.Id, dto);

        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_RemovesMessage_ReturnsDto()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);
        Message message = await CreateMessage(user, team, "To delete");

        MessageDTOPublic? result = await _service.DeleteAsync(message.Id);

        result.Should().NotBeNull();
        result!.Text.Should().Be("To delete");

        Message? deleted = await _context.Messages.FindAsync(message.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNull_WhenNotFound()
    {
        MessageDTOPublic? result = await _service.DeleteAsync(999);

        result.Should().BeNull();
    }

    #endregion

    #region DecryptionFailure

    [Fact]
    public async Task GetByIdAsync_ReturnsDecryptionFailed_WhenSecretChanged()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        (string encrypted, string iv) = _crypto.Encrypt("Secret message", "old-secret-that-no-longer-exists");
        string signature = _crypto.Sign("Secret message", user.Secret);

        Message message = new()
        {
            EncryptedText = encrypted,
            Iv = iv,
            Signature = signature,
            Sender = user,
            Team = team,
            Date = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        MessageDTOPublic? result = await _service.GetByIdAsync(message.Id);

        result.Should().NotBeNull();
        result!.Text.Should().Be("[Decryption failed]");
        result.SignatureVerified.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsInvalidFormat_WhenIvCorrupted()
    {
        User user = await CreateUser("user-1");
        Team team = await CreateTeam();
        await AddMember(user, team);

        Message message = new()
        {
            EncryptedText = "validbase64==",
            Iv = "not-valid-base64!!!",
            Signature = "sig",
            Sender = user,
            Team = team,
            Date = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        MessageDTOPublic? result = await _service.GetByIdAsync(message.Id);

        result.Should().NotBeNull();
        result!.Text.Should().Be("[Invalid message format]");
    }

    #endregion
}
