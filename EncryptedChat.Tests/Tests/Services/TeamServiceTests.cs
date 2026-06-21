using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace EncryptedChat.Tests;

public class TeamServiceTests : IDisposable
{
    private const string TestEncryptionKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

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
        BlindIndex blindIndex = new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = TestEncryptionKey })
            .Build());
        _service = new TeamService(_context, _friendServiceMock.Object, _presenceServiceMock.Object, blindIndex);
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
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> CreateTeam(string name, User admin, string role = Member.AdminRole)
    {
        Team team = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(" ", "-"),
        };
        team.Members.Add(new Member
        {
            Team = team,
            User = admin,
            UserId = admin.Id,
            Role = role
        });
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    #region MarkReadAsync

    [Fact]
    public async Task MarkReadAsync_Member_SetsLastReadAt_AndReturnsTimestamp()
    {
        User user = await CreateUser("reader");
        Team team = await CreateTeam("Readable", user);

        DateTime before = DateTime.UtcNow;
        DateTime? result = await _service.MarkReadAsync(user.Id, team.Id);

        result.Should().NotBeNull();
        result!.Value.Should().BeOnOrAfter(before);
        Member member = await _context.Members.FirstAsync(m => m.UserId == user.Id && m.TeamId == team.Id);
        member.LastReadAt.Should().NotBeNull();
        member.LastReadAt!.Value.Should().Be(result.Value);
    }

    [Fact]
    public async Task MarkReadAsync_NonMember_ReturnsNull_AndDoesNotWrite()
    {
        User owner = await CreateUser("owner");
        Team team = await CreateTeam("Private", owner);

        DateTime? result = await _service.MarkReadAsync("stranger", team.Id);

        result.Should().BeNull();
        Member member = await _context.Members.FirstAsync(m => m.UserId == owner.Id && m.TeamId == team.Id);
        member.LastReadAt.Should().BeNull();
    }

    #endregion

    #region SetMutedAsync

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetMutedAsync_Member_WritesFlag(bool muted)
    {
        User user = await CreateUser("muter");
        Team team = await CreateTeam("Mutable", user);
        // Start from the opposite state so the assertion is meaningful both ways.
        Member seed = await _context.Members.FirstAsync(m => m.UserId == user.Id && m.TeamId == team.Id);
        seed.IsMuted = !muted;
        await _context.SaveChangesAsync();

        bool ok = await _service.SetMutedAsync(user.Id, team.Id, muted);

        ok.Should().BeTrue();
        Member member = await _context.Members.FirstAsync(m => m.UserId == user.Id && m.TeamId == team.Id);
        member.IsMuted.Should().Be(muted);
    }

    [Fact]
    public async Task SetMutedAsync_NonMember_ReturnsFalse()
    {
        User owner = await CreateUser("owner2");
        Team team = await CreateTeam("Private2", owner);

        bool ok = await _service.SetMutedAsync("stranger", team.Id, true);

        ok.Should().BeFalse();
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_CreatesTeam_WithCreatorAsOwner()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto = new() { Name = "New Team", Admins = [], Members = [], MemberKeyShares = new() { [creator.Id] = "wrapped" } };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Team");
        result.Members.Should().ContainSingle(m => m.User!.Id == creator.Id && m.Role == Member.OwnerRole);
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
        TeamDTO dto = new() { Name = "  Trimmed  ", Admins = [], Members = [], MemberKeyShares = new() { [creator.Id] = "wrapped" } };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Trimmed");
    }

    [Fact]
    public async Task CreateAsync_AddsSpecifiedAdmins()
    {
        User creator = await CreateUser("creator");
        User admin2 = await CreateUser("admin2");
        TeamDTO dto = new() { Name = "Team", Admins = [admin2.Id], Members = [], MemberKeyShares = new() { [creator.Id] = "wrapped", [admin2.Id] = "wrapped2" } };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Members.Should().HaveCount(2);
        result.Members.Should().Contain(m => m.User!.Id == creator.Id && m.Role == Member.OwnerRole);
        result.Members.Should().Contain(m => m.User!.Id == admin2.Id && m.Role == Member.AdminRole);
    }

    [Fact]
    public async Task CreateAsync_AddsSpecifiedMembers()
    {
        User creator = await CreateUser("creator");
        User member = await CreateUser("member");
        TeamDTO dto = new() { Name = "Team", Admins = [], Members = [member.Id], MemberKeyShares = new() { [creator.Id] = "wrapped", [member.Id] = "wrapped2" } };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().NotBeNull();
        result!.Members.Should().Contain(m => m.User!.Id == member.Id && m.Role == Member.MemberRole);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueSlug()
    {
        User creator = await CreateUser("creator");
        TeamDTO dto1 = new() { Name = "Same Name", Admins = [], Members = [], MemberKeyShares = new() { [creator.Id] = "w1" } };
        TeamDTO dto2 = new() { Name = "Same Name", Admins = [], Members = [], MemberKeyShares = new() { [creator.Id] = "w2" } };

        TeamDTOPublic? team1 = await _service.CreateAsync(dto1, creator.Id);
        TeamDTOPublic? team2 = await _service.CreateAsync(dto2, creator.Id);

        team1!.Slug.Should().NotBe(team2!.Slug);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenKeyShareCoverageIncomplete()
    {
        // True E2E: every member (creator + listed members) must have a wrapped
        // key share. Here the member is missing one → creation must be rejected
        // with zero side effects rather than shipping a member who can't decrypt.
        User creator = await CreateUser("creator");
        User member = await CreateUser("member");
        TeamDTO dto = new() { Name = "Team", Admins = [], Members = [member.Id], MemberKeyShares = new() { [creator.Id] = "wrapped" } };

        TeamDTOPublic? result = await _service.CreateAsync(dto, creator.Id);

        result.Should().BeNull();
        (await _context.Teams.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateTeam_SameName_ProducesDistinctSlugs_ViaBlindIndex()
    {
        // Two teams with the same name: the first gets the base slug and its blind index;
        // the second must get a suffixed slug because CreateUniqueSlugAsync detects the
        // collision via SlugBlindIndex (uniqueness enforced through the blind index now
        // that Slug is encrypted in production).
        User creator = await CreateUser("creator-slug");
        TeamDTO dto1 = new() { Name = "Acme Corp", Admins = [], Members = [], MemberKeyShares = new() { [creator.Id] = "w1" } };
        TeamDTO dto2 = new() { Name = "Acme Corp", Admins = [], Members = [], MemberKeyShares = new() { [creator.Id] = "w2" } };

        TeamDTOPublic? team1 = await _service.CreateAsync(dto1, creator.Id);
        TeamDTOPublic? team2 = await _service.CreateAsync(dto2, creator.Id);

        team1.Should().NotBeNull();
        team2.Should().NotBeNull();
        string slug1 = team1!.Slug;
        string slug2 = team2!.Slug;
        slug1.Should().NotBe(slug2);
        slug2.Should().StartWith("acme-corp");
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
    public async Task DeleteAsync_DeletesTeam_WhenOwner()
    {
        User owner = await CreateUser("owner");
        Team team = await CreateTeam("To Delete", owner, Member.OwnerRole);

        TeamDTOPublic? result = await _service.DeleteAsync(team.Id, owner.Id);

        result.Should().NotBeNull();
        (await _context.Teams.FindAsync(team.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNull_WhenAdminNotOwner()
    {
        // Owner-only action: an Admin (not Owner) must be denied.
        User owner = await CreateUser("owner");
        User admin = await CreateUser("admin");
        Team team = await CreateTeam("Team", owner, Member.OwnerRole);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = admin.Id, Role = Member.AdminRole });
        await _context.SaveChangesAsync();

        TeamDTOPublic? result = await _service.DeleteAsync(team.Id, admin.Id);

        result.Should().BeNull();
        (await _context.Teams.FindAsync(team.Id)).Should().NotBeNull();
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

    [Fact]
    public async Task DeleteAsync_RemovesNonCascadingChildren_WhenOwner()
    {
        // UserTeamPreferences (ClientCascade) and PinnedMessages (NoAction) don't
        // cascade at the DB level. On SQL Server, leaving them blocks the team
        // delete with a FK REFERENCE constraint (the production 500). DeleteAsync
        // must remove them explicitly. (InMemory ignores FKs, so we assert the
        // observable behaviour: no orphaned children remain after delete.)
        User owner = await CreateUser("owner-children");
        Team team = await CreateTeam("Team With Children", owner, Member.OwnerRole);

        _context.UserTeamPreferences.Add(new UserTeamPreference
        {
            UserId = owner.Id,
            TeamId = team.Id,
            BubbleColor = "#ff8800"
        });

        Message msg = new()
        {
            EncryptedText = "x",
            Iv = "iv",
            Signature = "sig",
            Sender = owner,
            Team = team,
            KeyGeneration = 1
        };
        _context.Messages.Add(msg);
        _context.PinnedMessages.Add(new PinnedMessage
        {
            Team = team,
            MessageId = msg.Id,
            Message = msg,
            PinnedById = owner.Id,
            PinnedBy = owner
        });
        await _context.SaveChangesAsync();

        // Detach the seeded children so EF's in-memory ClientCascade can't delete
        // them implicitly — this reproduces a real request (fresh context, children
        // not tracked) where only an explicit removal in DeleteAsync prevents the
        // orphan / FK violation.
        _context.ChangeTracker.Clear();

        TeamDTOPublic? result = await _service.DeleteAsync(team.Id, owner.Id);

        result.Should().NotBeNull();
        (await _context.UserTeamPreferences.CountAsync(p => p.TeamId == team.Id))
            .Should().Be(0);
        (await _context.PinnedMessages.CountAsync(p => p.TeamId == team.Id))
            .Should().Be(0);
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
    public async Task PromoteToAdminAsync_PromotesMember_WhenOwner()
    {
        User owner = await CreateUser("owner");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", owner, Member.OwnerRole);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.PromoteToAdminAsync(team.Id, member.Id, owner.Id);

        result.Should().BeTrue();
        Member? promoted = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == member.Id);
        promoted!.Role.Should().Be(Member.AdminRole);
    }

    [Fact]
    public async Task PromoteToAdminAsync_ReturnsFalse_WhenAdminNotOwner()
    {
        // Owner-only action: an Admin cannot promote other members.
        User owner = await CreateUser("owner");
        User admin = await CreateUser("admin");
        User member = await CreateUser("member");
        Team team = await CreateTeam("Team", owner, Member.OwnerRole);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = admin.Id, Role = Member.AdminRole });
        _context.Members.Add(new Member { TeamId = team.Id, UserId = member.Id, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        bool result = await _service.PromoteToAdminAsync(team.Id, member.Id, admin.Id);

        result.Should().BeFalse();
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
    public async Task DemoteFromAdminAsync_DemotesAdmin_WhenOwner()
    {
        User owner = await CreateUser("owner");
        User admin2 = await CreateUser("admin2");
        Team team = await CreateTeam("Team", owner, Member.OwnerRole);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = admin2.Id, Role = Member.AdminRole });
        await _context.SaveChangesAsync();

        bool result = await _service.DemoteFromAdminAsync(team.Id, admin2.Id, owner.Id);

        result.Should().BeTrue();
        Member? demoted = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == admin2.Id);
        demoted!.Role.Should().Be(Member.MemberRole);
    }

    [Fact]
    public async Task DemoteFromAdminAsync_ReturnsFalse_WhenAdminNotOwner()
    {
        // Owner-only action: an Admin cannot demote another Admin.
        User owner = await CreateUser("owner");
        User admin1 = await CreateUser("admin1");
        User admin2 = await CreateUser("admin2");
        Team team = await CreateTeam("Team", owner, Member.OwnerRole);
        _context.Members.Add(new Member { TeamId = team.Id, UserId = admin1.Id, Role = Member.AdminRole });
        _context.Members.Add(new Member { TeamId = team.Id, UserId = admin2.Id, Role = Member.AdminRole });
        await _context.SaveChangesAsync();

        bool result = await _service.DemoteFromAdminAsync(team.Id, admin2.Id, admin1.Id);

        result.Should().BeFalse();
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
}
