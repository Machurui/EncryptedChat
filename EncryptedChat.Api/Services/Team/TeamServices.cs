using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EncryptedChat.Services;

public class TeamService(EncryptedChatContext context, IFriendService friendService, IPresenceService presenceService, IBlindIndex blindIndex) : ITeamService
{
    private readonly EncryptedChatContext _context = context;
    private readonly IFriendService _friendService = friendService;
    private readonly IPresenceService _presenceService = presenceService;
    private readonly IBlindIndex _blindIndex = blindIndex;

    public async Task<IEnumerable<TeamDTOPublic?>?> GetAllAsync()
    {
        // Return a list of teams
        List<Team> teams = await _context.Teams
        .Include(t => t.Members)
            .ThenInclude(m => m.User)
        .AsNoTracking()
        .ToListAsync();

        return teams.Select(team => ItemToDTO(team));
    }

    public async Task<TeamDTOPublic?> GetByIdAsync(Guid id)
    {
        // Return a team by id
        Team? team = await _context.Teams
        .Include(t => t.Members)
            .ThenInclude(m => m.User)
        .AsNoTracking()
        .Where(t => t.Id == id)
        .SingleOrDefaultAsync();

        return team is null ? null : ItemToDTO(team);
    }

    public async Task<TeamDTOPublic?> GetTeamByUrlTokenAsync(string token, string userId)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userId))
            return null;

        Member? member = await _context.Members
            .Include(m => m.Team)
                .ThenInclude(t => t!.Members)
                    .ThenInclude(mm => mm.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UrlToken == token && m.UserId == userId);

        if (member?.Team == null)
            return null;

        return ItemToDTO(member.Team, userId);
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
            SlugBlindIndex = _blindIndex.Compute(slug, "slug"),
            Glyph = glyph,
            Color = color,
            MessageLifetime = messageLifetime,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        foreach (User admin in admins)
        {
            bool isCreator = admin.Id == creatorId;
            team.Members.Add(new Member
            {
                Team = team,
                User = admin,
                UserId = admin.Id,
                Role = isCreator ? Member.OwnerRole : Member.AdminRole,
                UrlToken = await GenerateUniqueUrlTokenAsync()
            });
        }

        foreach (User member in members.Where(member => admins.All(admin => admin.Id != member.Id)))
        {
            team.Members.Add(new Member
            {
                Team = team,
                User = member,
                UserId = member.Id,
                Role = Member.MemberRole,
                UrlToken = await GenerateUniqueUrlTokenAsync()
            });
        }

        HashSet<string> teamMemberIds = team.Members.Select(m => m.UserId).ToHashSet();
        Dictionary<string, string> providedShares = newTeam.MemberKeyShares ?? new();

        if (providedShares.Count == 0 && !string.IsNullOrEmpty(newTeam.InitialKeyShare))
            providedShares = new Dictionary<string, string> { [creatorId] = newTeam.InitialKeyShare };

        if (!teamMemberIds.SetEquals(providedShares.Keys))
            return null;

        if (providedShares.Values.Any(string.IsNullOrEmpty))
            return null;

        _context.Teams.Add(team);

        foreach ((string memberId, string wrappedKey) in providedShares)
        {
            _context.TeamKeyShares.Add(new TeamKeyShare
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                MemberId = memberId,
                Generation = team.KeyGeneration,
                WrappedKey = wrappedKey,
                CreatedAt = DateTime.UtcNow
            });
        }

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

        List<User> admins = await _context.Users
            .Where(u => team.Admins.Contains(u.Id))
            .ToListAsync();

        List<User> members = (team.Members != null && team.Members.Count != 0)
            ? await _context.Users.Where(u => team.Members.Contains(u.Id)).ToListAsync()
            : [];

        _context.Members.RemoveRange(teamToUpdate.Members);
        teamToUpdate.Members.Clear();

        foreach (User admin in admins)
        {
            teamToUpdate.Members.Add(new Member
            {
                Team = teamToUpdate,
                User = admin,
                UserId = admin.Id,
                Role = Member.AdminRole,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                UrlToken = await GenerateUniqueUrlTokenAsync()
            });
        }

        foreach (User member in members.Where(member => admins.All(admin => admin.Id != member.Id)))
        {
            teamToUpdate.Members.Add(new Member
            {
                Team = teamToUpdate,
                User = member,
                UserId = member.Id,
                Role = Member.MemberRole,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                UrlToken = await GenerateUniqueUrlTokenAsync()
            });
        }

        teamToUpdate.Name = trimmedName;
        SetTeamSlug(teamToUpdate, await CreateUniqueSlugAsync(trimmedName, teamToUpdate.Id));
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
        SetTeamSlug(team, await CreateUniqueSlugAsync(name, id));
        team.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ItemToDTO(team);
    }

    public async Task<TeamDTOPublic?> DeleteAsync(Guid id, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        if (!await IsOwnerAsync(actorId, id))
            return null;

        Team? teamToDelete = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teamToDelete == null)
            return null;

        List<UserTeamPreference> prefs = await _context.UserTeamPreferences
            .Where(p => p.TeamId == id)
            .ToListAsync();
        _context.UserTeamPreferences.RemoveRange(prefs);

        List<PinnedMessage> pins = await _context.PinnedMessages
            .Where(p => p.TeamId == id)
            .ToListAsync();
        _context.PinnedMessages.RemoveRange(pins);

        _context.Teams.Remove(teamToDelete);
        await _context.SaveChangesAsync();

        return ItemToDTO(teamToDelete);
    }

    public async Task<bool> IsAdminAsync(string userId, Guid teamId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        // Owner counts as admin for permission checks that allow either —
        // anyone "admin or above" passes IsAdminAsync.
        return await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId
                           && m.UserId == userId
                           && (m.Role == Member.AdminRole || m.Role == Member.OwnerRole));
    }

    public async Task<bool> IsOwnerAsync(string userId, Guid teamId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId
                           && m.UserId == userId
                           && m.Role == Member.OwnerRole);
    }

    public async Task<bool> IsMemberAsync(string userId, Guid teamId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId);
    }

    public async Task<DateTime?> MarkReadAsync(string userId, Guid teamId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        Member? member = await _context.Members
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TeamId == teamId);
        if (member == null)
            return null;

        DateTime now = DateTime.UtcNow;
        member.LastReadAt = now;
        member.ModifiedAt = now;
        await _context.SaveChangesAsync();
        return now;
    }

    public async Task<bool> SetMutedAsync(string userId, Guid teamId, bool muted)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        Member? member = await _context.Members
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TeamId == teamId);
        if (member == null)
            return false;

        member.IsMuted = muted;
        member.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
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

        Team? team = await _context.Teams.FindAsync(teamId);
        if (team is null)
            return false;

        User? user = await _context.Users.FindAsync(userId);
        if (user is null)
            return false;

        // Check if already a member
        Member? existingMember = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);
        if (existingMember is not null)
            return false;

        _context.Members.Add(new Member
        {
            TeamId = teamId,
            UserId = userId,
            Role = Member.MemberRole,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            UrlToken = await GenerateUniqueUrlTokenAsync()
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid teamId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actorId))
            return false;

        Member? actorMembership = await _context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == actorId);
        if (actorMembership == null)
            return false;

        Member? member = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);
        if (member is null)
            return false;

        bool actorIsOwner = actorMembership.Role == Member.OwnerRole;
        bool actorIsAdmin = actorMembership.Role == Member.AdminRole;

        if (!actorIsOwner && !actorIsAdmin)
            return false;

        if (userId == actorId)
            return false;

        if (member.Role == Member.OwnerRole)
            return false;

        if (actorIsAdmin && member.Role != Member.MemberRole)
            return false;

        if (member.Role == Member.AdminRole)
        {
            int adminCount = await _context.Members.CountAsync(m => m.TeamId == teamId && m.Role == Member.AdminRole);
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

        if (!await IsOwnerAsync(actorId, teamId))
            return false;

        Member? member = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            return false;

        if (member.Role == Member.AdminRole || member.Role == Member.OwnerRole)
            return false;

        member.Role = Member.AdminRole;
        member.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DemoteFromAdminAsync(Guid teamId, string userId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actorId))
            return false;

        if (!await IsOwnerAsync(actorId, teamId))
            return false;

        if (userId == actorId)
            return false;

        Member? member = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            return false;

        if (member.Role != Member.AdminRole)
            return false;

        member.Role = Member.MemberRole;
        member.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TransferOwnershipAsync(Guid teamId, string fromUserId, string toUserId)
    {
        if (string.IsNullOrWhiteSpace(fromUserId) || string.IsNullOrWhiteSpace(toUserId))
            return false;

        if (fromUserId == toUserId)
            return false;

        Member? fromMember = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == fromUserId);
        if (fromMember == null || fromMember.Role != Member.OwnerRole)
            return false;

        Member? toMember = await _context.Members.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == toUserId);
        if (toMember == null)
            return false;

        if (toMember.Role != Member.AdminRole)
            return false;

        DateTime now = DateTime.UtcNow;
        fromMember.Role = Member.AdminRole;
        fromMember.ModifiedAt = now;
        toMember.Role = Member.OwnerRole;
        toMember.ModifiedAt = now;
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
                SetTeamSlug(team, await CreateUniqueSlugAsync(trimmedName, id));
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

    public async Task<TeamDTOPublic?> GetOrCreateDirectMessageAsync(string userId, string friendId, string? myWrappedKey = null, string? friendWrappedKey = null)
    {
        (TeamDTOPublic? dm, bool _) = await GetOrCreateDirectMessageWithStatusAsync(userId, friendId, myWrappedKey, friendWrappedKey);
        return dm;
    }

    public async Task<(TeamDTOPublic? Dm, bool IsNew)> GetOrCreateDirectMessageWithStatusAsync(string userId, string friendId, string? myWrappedKey = null, string? friendWrappedKey = null)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(friendId))
            return (null, false);

        if (userId == friendId)
            return (null, false);

        User? user = await _context.Users.FindAsync(userId);
        User? friend = await _context.Users.FindAsync(friendId);
        if (user == null || friend == null)
            return (null, false);

        // Find existing DM
        Team? existingDm = await _context.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .Where(t => t.IsDirect && t.Members.Count == 2)
            .Where(t => t.Members.Any(m => m.UserId == userId) && t.Members.Any(m => m.UserId == friendId))
            .FirstOrDefaultAsync();

        if (existingDm != null)
            return (ItemToDTO(existingDm), false);

        if (string.IsNullOrEmpty(myWrappedKey) || string.IsNullOrEmpty(friendWrappedKey))
            return (null, false);

        string dmSlug = $"dm-{Guid.NewGuid():N}";
        Team dm = new()
        {
            Id = Guid.NewGuid(),
            Name = $"{user.Name} & {friend.Name}",
            Slug = dmSlug,
            SlugBlindIndex = _blindIndex.Compute(dmSlug, "slug"),
            IsDirect = true,
            Glyph = "💬",
            Color = "oklch(0.65 0.16 165)",
            MessageLifetime = "off",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.Teams.Add(dm);
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = dm.Id, UserId = userId, Role = Member.MemberRole, UrlToken = await GenerateUniqueUrlTokenAsync() });
        _context.Members.Add(new Member { Id = Guid.NewGuid(), TeamId = dm.Id, UserId = friendId, Role = Member.MemberRole, UrlToken = await GenerateUniqueUrlTokenAsync() });

        _context.TeamKeyShares.Add(new TeamKeyShare
        {
            Id = Guid.NewGuid(),
            TeamId = dm.Id,
            MemberId = userId,
            Generation = dm.KeyGeneration,
            WrappedKey = myWrappedKey,
            CreatedAt = DateTime.UtcNow
        });

        _context.TeamKeyShares.Add(new TeamKeyShare
        {
            Id = Guid.NewGuid(),
            TeamId = dm.Id,
            MemberId = friendId,
            Generation = dm.KeyGeneration,
            WrappedKey = friendWrappedKey,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        Team? created = await _context.Teams
            .Include(t => t.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == dm.Id);

        return (created != null ? ItemToDTO(created) : null, true);
    }

    private bool TeamExists(Guid id)
    {
        return _context.Teams.Any(e => e.Id == id);
    }

    private TeamDTOPublic ItemToDTO(Team team, string? currentUserId = null)
    {
        UserDTOPublic MapUser(User user)
        {
            bool isOnline = _presenceService.IsOnline(user.Id);
            return new UserDTOPublic
            {
                Id = user.Id,
                Name = user.Name,
                Handle = user.Handle,
                Level = user.Level,
                NameColor = user.NameColor,
                ProfileImageUrl = user.ProfileImageUrl,
                Status = StatusHelper.EffectiveStatus(user.Status, isOnline),
                StatusMessage = (!isOnline || user.Status == "invisible") ? null : user.StatusMessage
            };
        }

        MemberDTOPublic MapMember(Member member) => new()
        {
            User = member.User is null ? null : MapUser(member.User),
            Role = member.Role
        };

        TeamDTOPublic dto = new()
        {
            Id = team.Id,
            Name = team.Name,
            Slug = team.Slug,
            Glyph = team.Glyph,
            Color = team.Color,
            MessageLifetime = team.MessageLifetime,
            IsDirect = team.IsDirect,
            KeyGeneration = team.KeyGeneration,
            Members = [.. (team.Members ?? Enumerable.Empty<Member>()).Select(MapMember)]
        };

        if (!string.IsNullOrEmpty(currentUserId))
        {
            Member? myMember = team.Members?.FirstOrDefault(m => m.UserId == currentUserId);
            if (myMember != null) dto.UrlToken = myMember.UrlToken;
        }

        return dto;
    }

    private async Task<string> GenerateUniqueUrlTokenAsync()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string token = TokenGenerator.Generate(10);
            bool exists = await _context.Members.AnyAsync(m => m.UrlToken == token);
            if (!exists) return token;
        }
        throw new InvalidOperationException("Could not generate unique URL token after 5 attempts.");
    }

    private void SetTeamSlug(Team team, string slug)
    {
        team.Slug = slug;
        team.SlugBlindIndex = _blindIndex.Compute(slug, "slug");
    }

    private async Task<string> CreateUniqueSlugAsync(string? name, Guid? excludeTeamId = null)
    {
        string baseSlug = CreateSlug(name);

        string baseIndex = _blindIndex.Compute(baseSlug, "slug");
        IQueryable<Team> existsQuery = _context.Teams.AsNoTracking().Where(t => t.SlugBlindIndex == baseIndex);
        if (excludeTeamId.HasValue)
            existsQuery = existsQuery.Where(t => t.Id != excludeTeamId.Value);

        if (!await existsQuery.AnyAsync())
            return baseSlug;

        // Slug exists, append a random suffix
        string suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{baseSlug}-{suffix}";
    }

    private static string CreateSlug(string? name)
    {
        string source = string.IsNullOrWhiteSpace(name) ? Guid.NewGuid().ToString("N") : name.Trim().ToLowerInvariant();
        string slug = Regex.Replace(source, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }
}
