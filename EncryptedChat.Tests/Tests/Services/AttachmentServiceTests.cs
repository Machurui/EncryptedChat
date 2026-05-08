using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace EncryptedChat.Tests;

public class AttachmentServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly Mock<IFileStorageService> _mockStorage;
    private readonly MimeTypeValidator _validator;
    private readonly CryptoService _crypto;
    private readonly AttachmentService _service;

    public AttachmentServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EncryptedChatContext(options);
        _crypto = new CryptoService();
        _mockStorage = new Mock<IFileStorageService>();

        var fileOptions = Options.Create(new FileStorageOptions
        {
            BasePath = "./test",
            MaxFileSizeBytes = 25 * 1024 * 1024,
            AllowedExtensions = [".txt", ".png", ".jpg", ".pdf", ".gif"]
        });

        _validator = new MimeTypeValidator(fileOptions);
        _service = new AttachmentService(_context, _crypto, _mockStorage.Object, _validator, fileOptions);
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

    private async Task<Team> CreateTeam(string name, User admin)
    {
        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(" ", "-"),
            Secret = Guid.NewGuid().ToString("N")
        };
        team.Members.Add(new Member
        {
            Team = team,
            User = admin,
            UserId = admin.Id,
            Role = Member.AdminRole
        });
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task<Message> CreateMessage(User sender, Team team)
    {
        (string encrypted, string iv) = _crypto.Encrypt("Test message", team.Secret);
        string signature = _crypto.Sign("Test message", sender.Secret);

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

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_CreatesAttachment_WhenValid()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);
        byte[] content = [1, 2, 3, 4, 5];

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");

        var (attachment, error, isForbidden) = await _service.CreateAsync(message.Id, "file.txt", "text/plain", content, user.Id);

        attachment.Should().NotBeNull();
        error.Should().BeNull();
        isForbidden.Should().BeFalse();
        attachment!.FileName.Should().Be("file.txt");
        attachment.Size.Should().Be(5);
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenContentEmpty()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);

        var (attachment, error, _) = await _service.CreateAsync(message.Id, "file.txt", "text/plain", [], user.Id);

        attachment.Should().BeNull();
        error.Should().Contain("vide");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenFileTooLarge()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);
        byte[] largeContent = new byte[30 * 1024 * 1024];

        var (attachment, error, _) = await _service.CreateAsync(message.Id, "file.txt", "text/plain", largeContent, user.Id);

        attachment.Should().BeNull();
        error.Should().Contain("volumineux");
    }

    [Fact]
    public async Task CreateAsync_ReturnsForbidden_WhenNotMember()
    {
        User admin = await CreateUser("admin");
        User outsider = await CreateUser("outsider");
        Team team = await CreateTeam("Team", admin);
        Message message = await CreateMessage(admin, team);
        byte[] content = [1, 2, 3];

        var (attachment, error, isForbidden) = await _service.CreateAsync(message.Id, "file.txt", "text/plain", content, outsider.Id);

        attachment.Should().BeNull();
        isForbidden.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenMessageNotFound()
    {
        User user = await CreateUser("user1");
        byte[] content = [1, 2, 3];

        var (attachment, error, _) = await _service.CreateAsync(Guid.NewGuid(), "file.txt", "text/plain", content, user.Id);

        attachment.Should().BeNull();
        error.Should().Contain("introuvable");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenValidationFails()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);
        byte[] content = [1, 2, 3];

        var (attachment, error, _) = await _service.CreateAsync(message.Id, "file.exe", "application/octet-stream", content, user.Id);

        attachment.Should().BeNull();
        error.Should().Contain("non autorisée");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ReturnsAttachment_WhenMember()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");

        var (created, _, _) = await _service.CreateAsync(message.Id, "test.txt", "text/plain", [1, 2, 3], user.Id);

        AttachmentDTOPublic? result = await _service.GetByIdAsync(created!.Id, user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotMember()
    {
        User admin = await CreateUser("admin");
        User outsider = await CreateUser("outsider");
        Team team = await CreateTeam("Team", admin);
        Message message = await CreateMessage(admin, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");

        var (created, _, _) = await _service.CreateAsync(message.Id, "test.txt", "text/plain", [1, 2, 3], admin.Id);

        AttachmentDTOPublic? result = await _service.GetByIdAsync(created!.Id, outsider.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        User user = await CreateUser("user1");

        AttachmentDTOPublic? result = await _service.GetByIdAsync(Guid.NewGuid(), user.Id);

        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_DeletesAttachment_WhenOwner()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");
        _mockStorage.Setup(s => s.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var (created, _, _) = await _service.CreateAsync(message.Id, "test.txt", "text/plain", [1, 2, 3], user.Id);

        bool result = await _service.DeleteAsync(created!.Id, user.Id);

        result.Should().BeTrue();
        (await _context.Attachments.FindAsync(created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DeletesAttachment_WhenAdmin()
    {
        User owner = await CreateUser("owner");
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = owner.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        Message message = await CreateMessage(owner, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");
        _mockStorage.Setup(s => s.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var (created, _, _) = await _service.CreateAsync(message.Id, "test.txt", "text/plain", [1, 2, 3], owner.Id);

        bool result = await _service.DeleteAsync(created!.Id, admin.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotOwnerNorAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        Message message = await CreateMessage(admin, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");

        var (created, _, _) = await _service.CreateAsync(message.Id, "test.txt", "text/plain", [1, 2, 3], admin.Id);

        bool result = await _service.DeleteAsync(created!.Id, member.Id);

        result.Should().BeFalse();
        (await _context.Attachments.FindAsync(created.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        User user = await CreateUser("user1");

        bool result = await _service.DeleteAsync(Guid.NewGuid(), user.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region GetByMessageIdAsync

    [Fact]
    public async Task GetByMessageIdAsync_ReturnsAttachments_WhenMember()
    {
        User user = await CreateUser("user1");
        Team team = await CreateTeam("Team", user);
        Message message = await CreateMessage(user, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");

        await _service.CreateAsync(message.Id, "file1.txt", "text/plain", [1, 2], user.Id);
        await _service.CreateAsync(message.Id, "file2.txt", "text/plain", [3, 4], user.Id);

        IReadOnlyList<AttachmentDTOPublic> result = await _service.GetByMessageIdAsync(message.Id, user.Id);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByMessageIdAsync_ReturnsEmpty_WhenNotMember()
    {
        User admin = await CreateUser("admin");
        User outsider = await CreateUser("outsider");
        Team team = await CreateTeam("Team", admin);
        Message message = await CreateMessage(admin, team);

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<byte[]>(), team.Id)).ReturnsAsync("path/file.enc");

        await _service.CreateAsync(message.Id, "file.txt", "text/plain", [1, 2, 3], admin.Id);

        IReadOnlyList<AttachmentDTOPublic> result = await _service.GetByMessageIdAsync(message.Id, outsider.Id);

        result.Should().BeEmpty();
    }

    #endregion
}
