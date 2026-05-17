using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EncryptedChat.Tests;

public class UserServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly UserManager<User> _userManager;
    private readonly UserService _service;
    private readonly Mock<ICryptoService> _cryptoMock;

    public UserServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EncryptedChatContext(options);
        _userManager = CreateUserManager(_context);
        _cryptoMock = new Mock<ICryptoService>();
        _cryptoMock.Setup(c => c.Decrypt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("Test message");
        _service = new UserService(_context, _userManager, _cryptoMock.Object);
    }

    public void Dispose()
    {
        _userManager.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static UserManager<User> CreateUserManager(EncryptedChatContext context)
    {
        UserStore<User> store = new(context);
        store.AutoSaveChanges = true;
        return new UserManager<User>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<User>>.Instance);
    }

    private async Task<User> CreateTestUser(string id = "user-1", string name = "TestUser", string email = "test@test.com")
    {
        User user = new()
        {
            Id = id,
            Name = name,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Level = 1,
            Secret = "secret"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<User> CreateAdminUser(string id = "admin-user")
    {
        User admin = await CreateTestUser(id, "AdminUser", "admin@test.com");
        _context.Roles.Add(new IdentityRole
        {
            Id = "admin-role",
            Name = "Admin",
            NormalizedName = "ADMIN"
        });
        _context.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = admin.Id,
            RoleId = "admin-role"
        });
        await _context.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task GetOwnProfileAsync_ReturnsUser_WithEmail()
    {
        User user = await CreateTestUser();

        UserProfileDTO? result = await _service.GetOwnProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetUserAsync_ReturnsSelf_WhenRequestingSelf()
    {
        User user = await CreateTestUser();

        UserDTOPublic? result = await _service.GetUserAsync(user.Id, user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Name.Should().Be(user.Name);
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenNotTeammates()
    {
        User user1 = await CreateTestUser("user-1", "User1", "user1@test.com");
        User user2 = await CreateTestUser("user-2", "User2", "user2@test.com");

        UserDTOPublic? result = await _service.GetUserAsync(user2.Id, user1.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_ReturnsUser_WhenTeammates()
    {
        User user1 = await CreateTestUser("user-1", "User1", "user1@test.com");
        User user2 = await CreateTestUser("user-2", "User2", "user2@test.com");

        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = "SharedTeam",
            Slug = "shared-team",
            Secret = "team-secret"
        };
        _context.Teams.Add(team);
        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            Team = team,
            TeamId = team.Id,
            User = user1,
            UserId = user1.Id,
            Role = Member.MemberRole
        });
        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            Team = team,
            TeamId = team.Id,
            User = user2,
            UserId = user2.Id,
            Role = Member.MemberRole
        });
        await _context.SaveChangesAsync();

        UserDTOPublic? result = await _service.GetUserAsync(user2.Id, user1.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user2.Id);
        result.Name.Should().Be(user2.Name);
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenUserIdIsEmpty()
    {
        User user = await CreateTestUser();

        UserDTOPublic? result = await _service.GetUserAsync("", user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenRequesterIdIsEmpty()
    {
        User user = await CreateTestUser();

        UserDTOPublic? result = await _service.GetUserAsync(user.Id, "");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        User user = await CreateTestUser();

        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = "Team",
            Slug = "team",
            Secret = "team-secret"
        };
        _context.Teams.Add(team);
        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            Team = team,
            TeamId = team.Id,
            User = user,
            UserId = user.Id,
            Role = Member.MemberRole
        });
        await _context.SaveChangesAsync();

        UserDTOPublic? result = await _service.GetUserAsync("nonexistent", user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserTeamsAsync_ReturnsEmpty_WhenNotOwner()
    {
        User user = await CreateTestUser();

        IReadOnlyList<UserTeamDTO> result = await _service.GetUserTeamsAsync(user.Id, "other-user");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTeamsAsync_ReturnsCompactTeams_WhenOwner()
    {
        User user = await CreateTestUser();
        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = "Team1",
            Slug = "team1",
            Secret = "team-secret"
        };

        _context.Teams.Add(team);
        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            Team = team,
            TeamId = team.Id,
            User = user,
            UserId = user.Id,
            Role = Member.AdminRole
        });
        await _context.SaveChangesAsync();

        IReadOnlyList<UserTeamDTO> result = await _service.GetUserTeamsAsync(user.Id, user.Id);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(team.Id);
        result[0].Name.Should().Be(team.Name);
        result[0].Slug.Should().Be(team.Slug);
        result[0].Role.Should().Be(Member.AdminRole);
    }

    [Fact]
    public async Task GetUserTeamsAsync_RespectsPageSize()
    {
        User user = await CreateTestUser();

        for (int i = 0; i < 5; i++)
        {
            Team team = new()
            {
                Id = Guid.NewGuid(),
                Name = $"Team{i}",
                Slug = $"team{i}",
                Secret = "secret"
            };
            _context.Teams.Add(team);
            _context.Members.Add(new Member
            {
                Id = Guid.NewGuid(),
                Team = team,
                TeamId = team.Id,
                User = user,
                UserId = user.Id,
                Role = Member.MemberRole
            });
        }
        await _context.SaveChangesAsync();

        IReadOnlyList<UserTeamDTO> result = await _service.GetUserTeamsAsync(user.Id, user.Id, page: 1, pageSize: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserTeamsAsync_ClampsPageSizeToMax()
    {
        User user = await CreateTestUser();

        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = "Team1",
            Slug = "team1",
            Secret = "secret"
        };
        _context.Teams.Add(team);
        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            Team = team,
            TeamId = team.Id,
            User = user,
            UserId = user.Id,
            Role = Member.MemberRole
        });
        await _context.SaveChangesAsync();

        IReadOnlyList<UserTeamDTO> result = await _service.GetUserTeamsAsync(user.Id, user.Id, page: 1, pageSize: 1000);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotOwner()
    {
        User user = await CreateTestUser();
        UserUpdateDTO dto = new() { Name = "NewName" };

        UserUpdateResult result = await _service.UpdateAsync(user.Id, "other-user", dto);

        result.Status.Should().Be(UserOperationStatus.Forbidden);
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesName_WhenOwner()
    {
        User user = await CreateTestUser();
        UserUpdateDTO dto = new() { Name = "NewName" };

        UserUpdateResult result = await _service.UpdateAsync(user.Id, user.Id, dto);

        result.Status.Should().Be(UserOperationStatus.Success);
        result.User.Should().NotBeNull();
        result.User!.Name.Should().Be("NewName");

        User? updated = await _context.Users.FindAsync(user.Id);
        updated!.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEmail_WhenOwner()
    {
        User user = await CreateTestUser();
        UserUpdateDTO dto = new() { Email = "newemail@test.com" };

        UserUpdateResult result = await _service.UpdateAsync(user.Id, user.Id, dto);

        result.Status.Should().Be(UserOperationStatus.Success);
        result.User.Should().NotBeNull();
        result.User!.Email.Should().Be("newemail@test.com");

        User? updated = await _context.Users.FindAsync(user.Id);
        updated!.Email.Should().Be("newemail@test.com");
        updated.UserName.Should().Be("newemail@test.com");
        updated.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNameAlreadyExists()
    {
        await CreateTestUser("1", "ExistingName", "user1@test.com");
        User user = await CreateTestUser("2", "MyName", "user2@test.com");

        UserUpdateDTO dto = new() { Name = "ExistingName" };

        UserUpdateResult result = await _service.UpdateAsync(user.Id, user.Id, dto);

        result.Status.Should().Be(UserOperationStatus.Conflict);
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenEmailAlreadyExists()
    {
        await CreateTestUser("1", "User1", "existing@test.com");
        User user = await CreateTestUser("2", "User2", "user2@test.com");

        UserUpdateDTO dto = new() { Email = "existing@test.com" };

        UserUpdateResult result = await _service.UpdateAsync(user.Id, user.Id, dto);

        result.Status.Should().Be(UserOperationStatus.Conflict);
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsValidationFailed_WhenDtoIsEmpty()
    {
        User user = await CreateTestUser();
        UserUpdateDTO dto = new();

        UserUpdateResult result = await _service.UpdateAsync(user.Id, user.Id, dto);

        result.Status.Should().Be(UserOperationStatus.ValidationFailed);
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenDeleted()
    {
        User user = await CreateTestUser();
        User admin = await CreateAdminUser();

        UserDeleteResult result = await _service.DeleteAsync(user.Id, admin.Id);

        result.Status.Should().Be(UserOperationStatus.Success);
        User? deleted = await _context.Users.FindAsync(user.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        User admin = await CreateAdminUser();

        UserDeleteResult result = await _service.DeleteAsync("nonexistent", admin.Id);

        result.Status.Should().Be(UserOperationStatus.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenRequesterIsNotAdmin()
    {
        User user = await CreateTestUser();

        UserDeleteResult result = await _service.DeleteAsync(user.Id, "normal-user");

        result.Status.Should().Be(UserOperationStatus.Forbidden);
        (await _context.Users.FindAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsValidationFailed_WhenDeletingSelf()
    {
        User user = await CreateTestUser();

        UserDeleteResult result = await _service.DeleteAsync(user.Id, user.Id);

        result.Status.Should().Be(UserOperationStatus.ValidationFailed);
        (await _context.Users.FindAsync(user.Id)).Should().NotBeNull();
    }
}
