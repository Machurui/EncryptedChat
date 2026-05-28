using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EncryptedChat.Tests;

public class TeamServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly TeamService _service;
    private readonly Mock<IFriendService> _friendServiceMock;
    private readonly Mock<IPresenceService> _presenceServiceMock;

    public TeamServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EncryptedChatContext(options);
        _friendServiceMock = new Mock<IFriendService>();
        _friendServiceMock.Setup(f => f.AreFriendsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _presenceServiceMock = new Mock<IPresenceService>();
        _presenceServiceMock.Setup(p => p.IsOnline(It.IsAny<string>())).Returns(false);
        _service = new TeamService(_context, _friendServiceMock.Object, _presenceServiceMock.Object);
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

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_CreatesTeam_WithCreatorAsAdmin()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto = new() { Name = "New Team", Admins = [], Members = [] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Team");
        result.Members.Should().ContainSingle(m => m.User!.Id == creator.Id && m.Role == Member.AdminRole);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenCreatorNotFound()
    {
        TeamDTO dto = new() { Name = "Team", Admins = [], Members = [] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, "nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenNameTooShort()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto = new() { Name = "", Admins = [], Members = [] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenNameTooLong()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto = new() { Name = new string('x', 101), Admins = [], Members = [] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_TrimsName()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto = new() { Name = "  Trimmed  ", Admins = [], Members = [] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Trimmed");
    }

    [Fact]
    public async Task CreateAsync_AddsSpecifiedAdmins()
    {
        User creator = await CreateUser("creator");
        User admin2 = await CreateUser("admin2");
        TeamDTO dto = new() { Name = "Team", Admins = [admin2.Id], Members = [] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Members.Should().HaveCount(2);
        result.Members.Should().Contain(m => m.User!.Id == creator.Id && m.Role == Member.AdminRole);
        result.Members.Should().Contain(m => m.User!.Id == admin2.Id && m.Role == Member.AdminRole);
    }

    [Fact]
    public async Task CreateAsync_AddsSpecifiedMembers()
    {
        User creator = await CreateUser("creator");
        User member = await CreateUser("member");
        TeamDTO dto = new() { Name = "Team", Admins = [], Members = [member.Id] };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Members.Should().Contain(m => m.User!.Id == member.Id && m.Role == Member.MemberRole);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueSlug()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto1 = new() { Name = "Same Name", Admins = [], Members = [] };
        TeamDTO dto2 = new() { Name = "Same Name", Admins = [], Members = [] };

        TeamDTOPublic? team1 = await _service.CreateAsync(dto1, creator.Id);
        TeamDTOPublic? team2 = await _service.CreateAsync(dto2, creator.Id);

        team1!.Slug.Should().NotBe(team2!.Slug);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ReturnsTeam_WhenExists()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Test Team", admin);

        TeamDTOPublic? result = await _service.GetByIdAsync(team.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(team.Id);
        result.Name.Should().Be("Test Team");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        TeamDTOPublic? result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    #endregion

    #region UpdateNameAsync

    [Fact]
    public async Task UpdateNameAsync_UpdatesName_WhenAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Old Name", admin);

        TeamDTOPublic? result = await _service.UpdateNameAsync(team.Id, "New Name", admin.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateNameAsync_ReturnsNull_WhenNotAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        TeamDTOPublic? result = await _service.UpdateNameAsync(team.Id, "New Name", member.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateNameAsync_ReturnsNull_WhenNameInvalid()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        TeamDTOPublic? result = await _service.UpdateNameAsync(team.Id, "", admin.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateNameAsync_ReturnsNull_WhenTeamNotFound()
    {
        User admin = await CreateUser("admin");

        TeamDTOPublic? result = await _service.UpdateNameAsync(Guid.NewGuid(), "Name", admin.Id);

        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_DeletesTeam_WhenAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("To Delete", admin);

        TeamDTOPublic? result = await _service.DeleteAsync(team.Id, admin.Id);

        result.Should().NotBeNull();
        (await _context.Teams.FindAsync(team.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNull_WhenNotAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        TeamDTOPublic? result = await _service.DeleteAsync(team.Id, member.Id);

        result.Should().BeNull();
        (await _context.Teams.FindAsync(team.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNull_WhenTeamNotFound()
    {
        User admin = await CreateUser("admin");

        TeamDTOPublic? result = await _service.DeleteAsync(Guid.NewGuid(), admin.Id);

        result.Should().BeNull();
    }

    #endregion

    #region AddMemberAsync

    [Fact]
    public async Task AddMemberAsync_AddsMember_WhenAdmin()
    {
        User admin = await CreateUser("admin");
        User newMember = await CreateUser("newmember");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.AddMemberAsync(team.Id, newMember.Id, admin.Id);

        result.Should().BeTrue();
        (await _context.Members.AnyAsync(m => m.TeamId == team.Id && m.UserId == newMember.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenNotAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        User newMember = await CreateUser("newmember");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.AddMemberAsync(team.Id, newMember.Id, member.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenUserAlreadyMember()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.AddMemberAsync(team.Id, admin.Id, admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenUserNotFound()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.AddMemberAsync(team.Id, "nonexistent", admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenTeamNotFound()
    {
        User admin = await CreateUser("admin");
        User newMember = await CreateUser("newmember");

        bool result = await _service.AddMemberAsync(Guid.NewGuid(), newMember.Id, admin.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region RemoveMemberAsync

    [Fact]
    public async Task RemoveMemberAsync_RemovesMember_WhenAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.RemoveMemberAsync(team.Id, member.Id, admin.Id);

        result.Should().BeTrue();
        (await _context.Members.AnyAsync(m => m.TeamId == team.Id && m.UserId == member.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsFalse_WhenNotAdmin()
    {
        User admin = await CreateUser("admin");
        User member1 = await CreateUser("member1");
        User member2 = await CreateUser("member2");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member1.Id, Role = Member.MemberRole });
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member2.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.RemoveMemberAsync(team.Id, member2.Id, member1.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsFalse_WhenLastAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.RemoveMemberAsync(team.Id, admin.Id, admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsFalse_WhenMemberNotFound()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.RemoveMemberAsync(team.Id, "nonexistent", admin.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region PromoteToAdminAsync

    [Fact]
    public async Task PromoteToAdminAsync_PromotesMember_WhenAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.PromoteToAdminAsync(team.Id, member.Id, admin.Id);

        result.Should().BeTrue();
        Member? promoted = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == member.Id);
        promoted!.Role.Should().Be(Member.AdminRole);
    }

    [Fact]
    public async Task PromoteToAdminAsync_ReturnsFalse_WhenNotAdmin()
    {
        User admin = await CreateUser("admin");
        User member1 = await CreateUser("member1");
        User member2 = await CreateUser("member2");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member1.Id, Role = Member.MemberRole });
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member2.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.PromoteToAdminAsync(team.Id, member2.Id, member1.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task PromoteToAdminAsync_ReturnsFalse_WhenAlreadyAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.PromoteToAdminAsync(team.Id, admin.Id, admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task PromoteToAdminAsync_ReturnsFalse_WhenMemberNotFound()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.PromoteToAdminAsync(team.Id, "nonexistent", admin.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region DemoteFromAdminAsync

    [Fact]
    public async Task DemoteFromAdminAsync_DemotesAdmin_WhenAdmin()
    {
        User admin1 = await CreateUser("admin1");
        User admin2 = await CreateUser("admin2");
        Team team = await CreateTeam("Team", admin1);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = admin2.Id, Role = Member.AdminRole });
        await _context.SaveChangesAsync();

        bool result = await _service.DemoteFromAdminAsync(team.Id, admin2.Id, admin1.Id);

        result.Should().BeTrue();
        Member? demoted = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == admin2.Id);
        demoted!.Role.Should().Be(Member.MemberRole);
    }

    [Fact]
    public async Task DemoteFromAdminAsync_ReturnsFalse_WhenNotAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.DemoteFromAdminAsync(team.Id, admin.Id, member.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DemoteFromAdminAsync_ReturnsFalse_WhenLastAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.DemoteFromAdminAsync(team.Id, admin.Id, admin.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DemoteFromAdminAsync_ReturnsFalse_WhenNotAnAdmin()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.DemoteFromAdminAsync(team.Id, member.Id, admin.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region IsAdminAsync / IsMemberAsync

    [Fact]
    public async Task IsAdminAsync_ReturnsTrue_WhenUserIsAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.IsAdminAsync(admin.Id, team.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAdminAsync_ReturnsFalse_WhenUserIsMember()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.IsAdminAsync(member.Id, team.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_ReturnsFalse_WhenUserNotInTeam()
    {
        User admin = await CreateUser("admin");
        User other = await CreateUser("other");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.IsAdminAsync(other.Id, team.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberAsync_ReturnsTrue_WhenUserIsMember()
    {
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", admin);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.IsMemberAsync(member.Id, team.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMemberAsync_ReturnsTrue_WhenUserIsAdmin()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.IsMemberAsync(admin.Id, team.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMemberAsync_ReturnsFalse_WhenUserNotInTeam()
    {
        User admin = await CreateUser("admin");
        User other = await CreateUser("other");
        Team team = await CreateTeam("Team", admin);

        bool result = await _service.IsMemberAsync(other.Id, team.Id);

        result.Should().BeFalse();
    }

    #endregion

    #region UpdatePartialAsync OwnBubbleColor

    [Fact]
    public async Task UpdatePartialAsync_AcceptsValidOwnBubbleColor()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        TeamUpdateDTO dto = new() { OwnBubbleColor = "oklch(0.70 0.18 30)" };

        TeamDTOPublic? result = await _service.UpdatePartialAsync(team.Id, dto, admin.Id);

        result.Should().NotBeNull();
        result!.OwnBubbleColor.Should().Be("oklch(0.70 0.18 30)");

        Team? reloaded = await _context.Teams.FindAsync(team.Id);
        reloaded!.OwnBubbleColor.Should().Be("oklch(0.70 0.18 30)");
    }

    [Fact]
    public async Task UpdatePartialAsync_IgnoresInvalidOwnBubbleColor()
    {
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", admin);

        TeamUpdateDTO dto = new() { OwnBubbleColor = "not a color" };

        TeamDTOPublic? result = await _service.UpdatePartialAsync(team.Id, dto, admin.Id);

        result.Should().BeNull();

        Team? reloaded = await _context.Teams.FindAsync(team.Id);
        reloaded!.OwnBubbleColor.Should().BeNull();
    }

    #endregion
}
