using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace EncryptedChat.Services;

public class UserService(EncryptedChatContext context, UserManager<User> userManager, ICryptoService crypto, IPresenceService presenceService) : IUserService
{
    private readonly EncryptedChatContext _context = context;
    private readonly UserManager<User> _userManager = userManager;
    private readonly ICryptoService _crypto = crypto;
    private readonly IPresenceService _presenceService = presenceService;

    private static readonly System.Text.RegularExpressions.Regex HandleRegex =
        new(@"^[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<UserProfileDTO?> GetOwnProfileAsync(string id)
    {
        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return null;

        return new UserProfileDTO
        {
            Id = user.Id,
            Name = user.Name,
            Handle = user.Handle,
            Email = user.Email ?? string.Empty,
            Level = user.Level,
            NameColor = user.NameColor,
            ProfileImageUrl = user.ProfileImageUrl,
            Status = user.Status,
            StatusMessage = user.StatusMessage,
            Theme = user.Theme,
            ReadReceipts = user.ReadReceipts,
            TypingIndicators = user.TypingIndicators,
            NotificationPreference = user.NotificationPreference,
            LastSeenAt = user.LastSeenAt
        };
    }

    public async Task<UserDTOPublic?> GetUserAsync(string userId, string requesterId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(requesterId))
            return null;

        if (userId == requesterId)
        {
            User? self = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (self == null) return null;
            return new UserDTOPublic { Id = self.Id, Name = self.Name, Handle = self.Handle, Level = self.Level, NameColor = self.NameColor, ProfileImageUrl = self.ProfileImageUrl, Status = self.Status, StatusMessage = self.StatusMessage };
        }

        bool areTeammates = await _context.Members
            .AsNoTracking()
            .Where(m => m.UserId == requesterId)
            .SelectMany(m => m.Team!.Members)
            .AnyAsync(m => m.UserId == userId);

        if (!areTeammates)
            return null;

        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

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

    private const int MaxPageSize = 50;

    public async Task<IReadOnlyList<UserTeamDTO>> GetUserTeamsAsync(string userId, string requesterId, int page = 1, int pageSize = 20)
    {
        if (userId != requesterId)
            return [];

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        bool userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return [];

        var userTeams = await _context.Members
            .AsNoTracking()
            .Include(m => m.Team)
            .Where(m => m.UserId == userId && m.Team != null)
            .Select(m => new { m.Team!.Id, m.Team.Name, m.Team.Slug, m.Team.Glyph, m.Team.Color, m.Team.MessageLifetime, m.Team.IsDirect, m.Team.Secret, m.Role })
            .ToListAsync();

        var teamIds = userTeams.Select(t => t.Id).ToList();

        var lastMessages = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .Where(m => m.Team != null && teamIds.Contains(m.Team.Id))
            .GroupBy(m => m.Team!.Id)
            .Select(g => g.OrderByDescending(m => m.Date).FirstOrDefault())
            .ToListAsync();

        var lastMsgDict = lastMessages
            .Where(m => m != null)
            .ToDictionary(m => m!.Team!.Id, m => m!);

        List<UserTeamDTO> teams = userTeams.Select(t => new UserTeamDTO
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            Role = t.Role,
            Glyph = t.Glyph,
            Color = t.Color,
            MessageLifetime = t.MessageLifetime,
            IsDirect = t.IsDirect
        }).ToList();

        foreach (var team in teams)
        {
            var userTeam = userTeams.First(t => t.Id == team.Id);
            if (lastMsgDict.TryGetValue(team.Id, out var lastMsg) && lastMsg != null)
            {
                team.LastMessageTime = lastMsg.Date;
                team.LastMessageSenderName = lastMsg.Sender?.Name;

                try
                {
                    string plaintext = _crypto.Decrypt(lastMsg.EncryptedText, lastMsg.Iv, userTeam.Secret);
                    string preview = plaintext.Replace("\n", " ").Trim();
                    if (preview.Length > 50) preview = preview[..50] + "...";
                    team.LastMessagePreview = preview;
                }
                catch (CryptographicException)
                {
                    team.LastMessagePreview = "[Encrypted]";
                }
            }
        }

        teams = teams
            .OrderByDescending(t => t.LastMessageTime ?? DateTime.MinValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return teams;
    }

    public async Task<IReadOnlyList<UserDTOPublic>> SearchUsersAsync(string query, string requesterId, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(requesterId))
            return [];

        if (limit < 1) limit = 1;
        if (limit > 20) limit = 20;

        string normalizedQuery = query.Trim().ToLowerInvariant();
        if (normalizedQuery.Length < 2)
            return [];

        List<UserDTOPublic> users = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id != requesterId &&
                        (u.Name.ToLower().Contains(normalizedQuery) ||
                         (u.Handle != null && u.Handle.ToLower().Contains(normalizedQuery)) ||
                         (u.Email != null && u.Email.ToLower().Contains(normalizedQuery))))
            .OrderBy(u => u.Name)
            .Take(limit)
            .Select(u => new UserDTOPublic
            {
                Id = u.Id,
                Name = u.Name,
                Handle = u.Handle,
                Level = u.Level,
                NameColor = u.NameColor,
                ProfileImageUrl = u.ProfileImageUrl,
                Status = u.Status,
                StatusMessage = u.StatusMessage
            })
            .ToListAsync();

        foreach (var u in users)
        {
            bool isOnline = _presenceService.IsOnline(u.Id);
            u.Status = StatusHelper.EffectiveStatus(u.Status, isOnline);
            if (!isOnline || u.Status == "offline") u.StatusMessage = null;
        }

        return users;
    }

    public async Task<UserUpdateResult> UpdateAsync(string id, string requesterId, UserUpdateDTO dto)
    {
        if (id != requesterId)
            return new UserUpdateResult(UserOperationStatus.Forbidden);

        if (dto == null)
            return new UserUpdateResult(UserOperationStatus.ValidationFailed);

        string? name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim();
        string? handle = string.IsNullOrWhiteSpace(dto.Handle) ? null : dto.Handle.Trim().ToLowerInvariant();
        string? email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        string? nameColor = string.IsNullOrWhiteSpace(dto.NameColor) ? null : dto.NameColor.Trim();
        string? profileImageUrl = dto.ProfileImageUrl?.Trim();
        string? status = string.IsNullOrWhiteSpace(dto.Status) ? null : dto.Status.Trim().ToLowerInvariant();
        string? statusMessage = dto.StatusMessage;
        string? theme = string.IsNullOrWhiteSpace(dto.Theme) ? null : dto.Theme.Trim().ToLowerInvariant();

        if (name == null && handle == null && email == null && nameColor == null && profileImageUrl == null && status == null && statusMessage == null && theme == null && !dto.ReadReceipts.HasValue && !dto.TypingIndicators.HasValue && string.IsNullOrWhiteSpace(dto.NotificationPreference))
            return new UserUpdateResult(UserOperationStatus.ValidationFailed);

        User? user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return new UserUpdateResult(UserOperationStatus.NotFound);

        if (name != null)
        {
            if (name.Length < 2 || name.Length > 100)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);

            // Display name is free-form; no uniqueness constraint.
            user.Name = name;
        }

        if (handle != null && !string.Equals(handle, user.Handle, StringComparison.Ordinal))
        {
            if (user.Handle != null)
            {
                // Already claimed — handle is permanent.
                return new UserUpdateResult(UserOperationStatus.Forbidden);
            }

            // First-time claim path (user.Handle is null).
            if (handle.Length < 3 || handle.Length > 32)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);

            if (!HandleRegex.IsMatch(handle))
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);

            // First-time claim — user.Handle is null, so no self-exclusion needed.
            bool handleExists = await _context.Users.AnyAsync(u => u.Handle == handle);
            if (handleExists)
                return new UserUpdateResult(UserOperationStatus.Conflict);

            user.Handle = handle;
        }

        if (email != null)
        {
            if (email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);

            string normalizedEmail = _userManager.NormalizeEmail(email);
            bool emailExists = await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != id);
            if (emailExists)
                return new UserUpdateResult(UserOperationStatus.Conflict);

            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                user.EmailConfirmed = false;

            IdentityResult emailResult = await _userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);

            IdentityResult userNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!userNameResult.Succeeded)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
        }

        if (nameColor != null)
        {
            if (!CssColor.Regex.IsMatch(nameColor))
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
            user.NameColor = nameColor;
        }

        if (profileImageUrl != null)
        {
            if (profileImageUrl.Length > 500)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
            user.ProfileImageUrl = string.IsNullOrEmpty(profileImageUrl) ? null : profileImageUrl;
        }

        if (status != null)
        {
            string[] validStatuses = ["online", "away", "busy", "invisible"];
            if (!validStatuses.Contains(status))
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
            user.Status = status;
        }

        if (statusMessage != null)
        {
            if (statusMessage.Length > 100)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
            user.StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? null : statusMessage.Trim();
        }

        if (theme != null)
        {
            string[] validThemes = ["dark", "light", "auto"];
            if (!validThemes.Contains(theme))
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
            user.Theme = theme;
        }

        if (dto.ReadReceipts.HasValue)
        {
            user.ReadReceipts = dto.ReadReceipts.Value;
        }

        if (dto.TypingIndicators.HasValue)
        {
            user.TypingIndicators = dto.TypingIndicators.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.NotificationPreference))
        {
            string[] validPreferences = ["all", "mentions", "none"];
            string pref = dto.NotificationPreference.Trim().ToLowerInvariant();
            if (!validPreferences.Contains(pref))
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);
            user.NotificationPreference = pref;
        }

        IdentityResult updateResult;
        try
        {
            updateResult = await _userManager.UpdateAsync(user);
        }
        catch (DbUpdateException)
        {
            return new UserUpdateResult(UserOperationStatus.Conflict);
        }

        if (!updateResult.Succeeded)
            return updateResult.Errors.Any(e =>
                e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                ? new UserUpdateResult(UserOperationStatus.Conflict)
                : new UserUpdateResult(UserOperationStatus.ValidationFailed);

        return new UserUpdateResult(UserOperationStatus.Success, new UserProfileDTO
        {
            Id = user.Id,
            Name = user.Name,
            Handle = user.Handle,
            Email = user.Email ?? string.Empty,
            Level = user.Level,
            NameColor = user.NameColor,
            ProfileImageUrl = user.ProfileImageUrl,
            Status = user.Status,
            StatusMessage = user.StatusMessage,
            Theme = user.Theme,
            ReadReceipts = user.ReadReceipts,
            TypingIndicators = user.TypingIndicators,
            NotificationPreference = user.NotificationPreference
        });
    }

    public async Task<UserDeleteResult> DeleteAsync(string id, string requesterId)
    {
        if (string.IsNullOrWhiteSpace(requesterId))
            return new UserDeleteResult(UserOperationStatus.Forbidden);

        if (id == requesterId)
            return new UserDeleteResult(UserOperationStatus.ValidationFailed);

        User? requester = await _userManager.FindByIdAsync(requesterId);
        if (requester == null || !await _userManager.IsInRoleAsync(requester, "Admin"))
            return new UserDeleteResult(UserOperationStatus.Forbidden);

        User? user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return new UserDeleteResult(UserOperationStatus.NotFound);

        IdentityResult result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? new UserDeleteResult(UserOperationStatus.Success)
            : new UserDeleteResult(UserOperationStatus.ValidationFailed);
    }

    public async Task UpdateLastSeenAsync(string userId)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE AspNetUsers SET LastSeenAt = {0} WHERE Id = {1}",
            DateTime.UtcNow, userId);
    }

    public async Task<Dictionary<Guid, string>> GetOwnBubbleColorsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new Dictionary<Guid, string>();

        return await _context.UserTeamPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.BubbleColor != null)
            .ToDictionaryAsync(p => p.TeamId, p => p.BubbleColor!);
    }

    public async Task<UserOperationStatus> SetOwnBubbleColorAsync(string userId, Guid teamId, string? color)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return UserOperationStatus.NotFound;

        bool isMember = await _context.Members.AnyAsync(m => m.UserId == userId && m.TeamId == teamId);
        if (!isMember)
            return UserOperationStatus.Forbidden;

        string? normalized = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        if (normalized != null && !CssColor.IsValid(normalized))
            return UserOperationStatus.ValidationFailed;

        UserTeamPreference? pref = await _context.UserTeamPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TeamId == teamId);

        if (pref == null)
        {
            if (normalized == null)
                return UserOperationStatus.Success;

            _context.UserTeamPreferences.Add(new UserTeamPreference
            {
                UserId = userId,
                TeamId = teamId,
                BubbleColor = normalized
            });
        }
        else
        {
            pref.BubbleColor = normalized;
        }

        await _context.SaveChangesAsync();
        return UserOperationStatus.Success;
    }
}
