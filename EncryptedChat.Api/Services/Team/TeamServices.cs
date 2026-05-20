using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EncryptedChat.Services;

public class TeamService : ITeamService
{
    private readonly EncryptedChatContext _context;
    private readonly IFriendService _friendService;

    public TeamService(EncryptedChatContext context, IFriendService friendService)
    {
        _context = context;
        _friendService = friendService;
    }

    public async Task<IEnumerable<TeamDTOPublic?>?> GetAllAsync()
    {
        // Return a list of teams
        return await _context.Teams
        .Include(t => t.Members)
            .ThenInclude(m => m.User)
        .AsNoTracking()
        .Select(team => ItemToDTO(team))
        .ToListAsync();
    }

    public async Task<TeamDTOPublic?> GetByIdAsync(Guid id)
    {
        // Return a team by id
        return await _context.Teams
        .Include(t => t.Members)
            .ThenInclude(m => m.User)
        .AsNoTracking()
        .Where(t => t.Id == id)
        .Select(team => ItemToDTO(team))
        .SingleOrDefaultAsync();
    }

    public async Task<TeamDTOPublic?> CreateAsync(TeamDTO newTeam, string creatorId)
    {
        User? creator = await _context.Users.FindAsync(creatorId);
        if (creator == null)
            return null;

        string trimmedName = newTeam.Name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 1 || trimmedName.Length > 100)
            return null;

        HashSet<string> adminIds = new(newTeam.Admins ?? []) { creatorId };

        List<User> admins = await _context.Users
            .Where(u => adminIds.Contains(u.Id))
            .ToListAsync();

        List<User> members = newTeam.Members != null && newTeam.Members.Count != 0
            ? await _context.Users.Where(u => newTeam.Members.Contains(u.Id)).ToListAsync()
            : [];

        if (admins.Count == 0)
            return null;

        string slug = await CreateUniqueSlugAsync(trimmedName);

        string glyph = string.IsNullOrWhiteSpace(newTeam.Glyph) ? "◆" : newTeam.Glyph.Trim();
        if (!Team.ValidGlyphs.Contains(glyph))
            glyph = "◆";

        string color = string.IsNullOrWhiteSpace(newTeam.Color) ? Team.ValidColors[0] : newTeam.Color.Trim();
        if (!Team.ValidColors.Contains(color))
            color = Team.ValidColors[0];

        string messageLifetime = string.IsNullOrWhiteSpace(newTeam.MessageLifetime) ? "off" : newTeam.MessageLifetime.Trim().ToLowerInvariant();
        if (!Team.ValidMessageLifetimes.Contains(messageLifetime))
            messageLifetime = "off";

        Team team = new()
        {
            Name = trimmedName,
            Slug = slug,
            Secret = Guid.NewGuid().ToString("N"),
            Glyph = glyph,
            Color = color,
            MessageLifetime = messageLifetime,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        foreach (var admin in admins)
        {
            team.Members.Add(new Member
            {
                Team = team,
                User = admin,
                UserId = admin.Id,
                Role = Member.AdminRole
            });
        }

        foreach (var member in members.Where(member => admins.All(admin => admin.Id != member.Id)))
        {
            team.Members.Add(new Member
            {
                Team = team,
                User = member,
                UserId = member.Id,
                Role = Member.MemberRole
            });
        }

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return ItemToDTO(team);
    }

    public async Task<TeamDTOPublic?> UpdateAsync(Guid id, TeamDTO team, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        if (!await IsAdminAsync(actorId, id))
            return null;

        string trimmedName = team.Name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 1 || trimmedName.Length > 100)
            return null;

        if (team.Admins == null || team.Admins.Count == 0)
            return null;

        Team? teamToUpdate = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teamToUpdate == null)
            return null;

        var admins = await _context.Users
            .Where(u => team.Admins.Contains(u.Id))
            .ToListAsync();

        var members = (team.Members != null && team.Members.Count != 0)
            ? await _context.Users.Where(u => team.Members.Contains(u.Id)).ToListAsync()
            : [];

        _context.Members.RemoveRange(teamToUpdate.Members);
        teamToUpdate.Members.Clear();

        foreach (var admin in admins)
        {
            teamToUpdate.Members.Add(new Member
            {
                Team = teamToUpdate,
                User = admin,
                UserId = admin.Id,
                Role = Member.AdminRole,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            });
        }

        foreach (var member in members.Where(member => admins.All(admin => admin.Id != member.Id)))
        {
            teamToUpdate.Members.Add(new Member
            {
                Team = teamToUpdate,
                User = member,
                UserId = member.Id,
                Role = Member.MemberRole,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            });
        }

        teamToUpdate.Name = trimmedName;
        teamToUpdate.Slug = await CreateUniqueSlugAsync(trimmedName, teamToUpdate.Id);
        teamToUpdate.ModifiedAt = DateTime.UtcNow;

        try
        {
            if (!teamToUpdate.Members.Any(m => m.Role == Member.AdminRole))
                return null;

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TeamExists(id))
                return null;
            throw;
        }

        return ItemToDTO(teamToUpdate);
    }

    public async Task<TeamDTOPublic?> UpdateNameAsync(Guid id, string name, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        if (!await IsAdminAsync(actorId, id))
            return null;

        string trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 1 || trimmedName.Length > 100)
            return null;

        Team? team = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
            return null;

        team.Name = trimmedName;
        team.Slug = await CreateUniqueSlugAsync(name, id);
        team.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ItemToDTO(team);
    }

    public async Task<TeamDTOPublic?> DeleteAsync(Guid id, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        if (!await IsAdminAsync(actorId, id))
            return null;

        Team? teamToDelete = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teamToDelete == null)
            return null;

        _context.Teams.Remove(teamToDelete);
        await _context.SaveChangesAsync();

        return ItemToDTO(teamToDelete);
    }

    public async Task<bool> IsAdminAsync(string userId, Guid teamId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId
                           && m.UserId == userId
                           && m.Role == Member.AdminRole);
    }

    public async Task<bool> IsMemberAsync(string userId, Guid teamId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId);
    }

    public async Task<bool> AddMemberAsync(Guid teamId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actorId))
            return false;

        if (!await IsAdminAsync(actorId, teamId))
            return false;

        // Only friends can be added to a team
        if (!await _friendService.AreFriendsAsync(actorId, userId))
            return false;

        var team = await _context.Teams.FindAsync(teamId);
        if (team is null)
            return false;

        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            return false;

        // Check if already a member
        var existingMember = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);
        if (existingMember is not null)
            return false;

        _context.Members.Add(new Member
        {
            TeamId = teamId,
            UserId = userId,
            Role = Member.MemberRole,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid teamId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actorId))
            return false;

        if (!await IsAdminAsync(actorId, teamId))
            return false;

        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            return false;

        // Cannot remove if this is the last admin
        if (member.Role == Member.AdminRole)
        {
            var adminCount = await _context.Members
                .CountAsync(m => m.TeamId == teamId && m.Role == Member.AdminRole);
            if (adminCount <= 1)
                return false;
        }

        _context.Members.Remove(member);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PromoteToAdminAsync(Guid teamId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actorId))
            return false;

        if (!await IsAdminAsync(actorId, teamId))
            return false;

        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            return false;

        if (member.Role == Member.AdminRole)
            return false; // Already admin

        member.Role = Member.AdminRole;
        member.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DemoteFromAdminAsync(Guid teamId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actorId))
            return false;

        if (!await IsAdminAsync(actorId, teamId))
            return false;

        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            return false;

        if (member.Role != Member.AdminRole)
            return false; // Not an admin

        // Cannot demote if this is the last admin
        var adminCount = await _context.Members
            .CountAsync(m => m.TeamId == teamId && m.Role == Member.AdminRole);
        if (adminCount <= 1)
            return false;

        member.Role = Member.MemberRole;
        member.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<TeamDTOPublic?> UpdatePartialAsync(Guid id, TeamUpdateDTO dto, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        if (!await IsAdminAsync(actorId, id))
            return null;

        Team? team = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
            return null;

        bool hasChanges = false;

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            string trimmedName = dto.Name.Trim();
            if (trimmedName.Length >= 1 && trimmedName.Length <= 100)
            {
                team.Name = trimmedName;
                team.Slug = await CreateUniqueSlugAsync(trimmedName, id);
                hasChanges = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Glyph))
        {
            string glyph = dto.Glyph.Trim();
            if (Team.ValidGlyphs.Contains(glyph))
            {
                team.Glyph = glyph;
                hasChanges = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Color))
        {
            string color = dto.Color.Trim();
            if (Team.ValidColors.Contains(color))
            {
                team.Color = color;
                hasChanges = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.MessageLifetime))
        {
            string messageLifetime = dto.MessageLifetime.Trim().ToLowerInvariant();
            if (Team.ValidMessageLifetimes.Contains(messageLifetime))
            {
                team.MessageLifetime = messageLifetime;
                hasChanges = true;
            }
        }

        if (!hasChanges)
            return null;

        team.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ItemToDTO(team);
    }

    public async Task<IReadOnlyList<string>> GetMemberUserIdsAsync(Guid teamId)
    {
        return await _context.Members
            .AsNoTracking()
            .Where(m => m.TeamId == teamId)
            .Select(m => m.UserId)
            .ToListAsync();
    }

    public async Task<TeamDTOPublic?> GetOrCreateDirectMessageAsync(string userId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId))
            return null;

        if (userId == friendId)
            return null;

        // Check if both users exist
        var user = await _context.Users.FindAsync(userId);
        var friend = await _context.Users.FindAsync(friendId);
        if (user == null || friend == null)
            return null;

        // Find existing DM between these two users
        var existingDm = await _context.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .Where(t => t.IsDirect && t.Members.Count == 2)
            .Where(t => t.Members.Any(m => m.UserId == userId) && t.Members.Any(m => m.UserId == friendId))
            .FirstOrDefaultAsync();

        if (existingDm != null)
            return ItemToDTO(existingDm);

        // Create new DM
        var dmName = $"{user.Name} & {friend.Name}";
        var dm = new Team
        {
            Id = Guid.NewGuid(),
            Name = dmName,
            Slug = $"dm-{Guid.NewGuid():N}",
            Secret = Guid.NewGuid().ToString("N"),
            IsDirect = true,
            Glyph = "💬",
            Color = "oklch(0.65 0.16 165)",
            MessageLifetime = "off",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.Teams.Add(dm);

        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            TeamId = dm.Id,
            UserId = userId,
            Role = Member.MemberRole
        });

        _context.Members.Add(new Member
        {
            Id = Guid.NewGuid(),
            TeamId = dm.Id,
            UserId = friendId,
            Role = Member.MemberRole
        });

        await _context.SaveChangesAsync();

        // Reload with members
        var created = await _context.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == dm.Id);

        return created != null ? ItemToDTO(created) : null;
    }

    public async Task<(TeamDTOPublic? Dm, bool IsNew)> GetOrCreateDirectMessageWithStatusAsync(string userId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId))
            return (null, false);

        if (userId == friendId)
            return (null, false);

        var user = await _context.Users.FindAsync(userId);
        var friend = await _context.Users.FindAsync(friendId);
        if (user == null || friend == null)
            return (null, false);

        // Find existing DM
        var existingDm = await _context.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .Where(t => t.IsDirect && t.Members.Count == 2)
            .Where(t => t.Members.Any(m => m.UserId == userId) && t.Members.Any(m => m.UserId == friendId))
            .FirstOrDefaultAsync();

        if (existingDm != null)
            return (ItemToDTO(existingDm), false);

        // Create new DM
        var dm = new Team
        {
            Id = Guid.NewGuid(),
            Name = $"{user.Name} & {friend.Name}",
            Slug = $"dm-{Guid.NewGuid():N}",
            Secret = Guid.NewGuid().ToString("N"),
            IsDirect = true,
            Glyph = "💬",
            Color = "oklch(0.65 0.16 165)",
            MessageLifetime = "off",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.Teams.Add(dm);
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = dm.Id, UserId = userId, Role = Member.MemberRole });
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = dm.Id, UserId = friendId, Role = Member.MemberRole });
        await _context.SaveChangesAsync();

        var created = await _context.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == dm.Id);

        return (created != null ? ItemToDTO(created) : null, true);
    }

    private bool TeamExists(Guid id)
    {
        return _context.Teams.Any(e => e.Id == id);
    }

    private static TeamDTOPublic ItemToDTO(Team team)
    {
        static UserDTOPublic MapUser(User user) => new UserDTOPublic
        {
            Id = user.Id,
            Name = user.Name,
            Handle = user.Handle,
            Level = user.Level,
            NameColor = user.NameColor,
            ProfileImageUrl = user.ProfileImageUrl
        };

        static MemberDTOPublic MapMember(Member member) => new()
        {
            User = member.User is null ? null : MapUser(member.User),
            Role = member.Role
        };

        return new TeamDTOPublic
        {
            Id = team.Id,
            Name = team.Name,
            Slug = team.Slug,
            Glyph = team.Glyph,
            Color = team.Color,
            MessageLifetime = team.MessageLifetime,
            IsDirect = team.IsDirect,
            Members = [.. (team.Members ?? Enumerable.Empty<Member>()).Select(MapMember)]
        };
    }

    private async Task<string> CreateUniqueSlugAsync(string? name, Guid? excludeTeamId = null)
    {
        var baseSlug = CreateSlug(name);

        var existsQuery = _context.Teams.AsNoTracking().Where(t => t.Slug == baseSlug);
        if (excludeTeamId.HasValue)
            existsQuery = existsQuery.Where(t => t.Id != excludeTeamId.Value);

        if (!await existsQuery.AnyAsync())
            return baseSlug;

        // Slug exists, append a random suffix
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{baseSlug}-{suffix}";
    }

    private static string CreateSlug(string? name)
    {
        var source = string.IsNullOrWhiteSpace(name) ? Guid.NewGuid().ToString("N") : name.Trim().ToLowerInvariant();
        var slug = Regex.Replace(source, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }
}
