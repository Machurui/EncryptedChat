using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Services;

public class UserService(EncryptedChatContext context, UserManager<User> userManager) : IUserService
{
    private readonly EncryptedChatContext _context = context;
    private readonly UserManager<User> _userManager = userManager;

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
            Email = user.Email,
            Level = user.Level
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
            return new UserDTOPublic { Id = self.Id, Name = self.Name, Level = self.Level };
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

        return new UserDTOPublic
        {
            Id = user.Id,
            Name = user.Name,
            Level = user.Level
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

        List<UserTeamDTO> teams = await _context.Members
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Team != null)
            .OrderBy(m => m.Team!.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new UserTeamDTO
            {
                Id = m.Team!.Id,
                Name = m.Team.Name,
                Slug = m.Team.Slug,
                Role = m.Role
            })
            .ToListAsync();

        return teams;
    }

    public async Task<UserUpdateResult> UpdateAsync(string id, string requesterId, UserUpdateDTO dto)
    {
        if (id != requesterId)
            return new UserUpdateResult(UserOperationStatus.Forbidden);

        if (dto == null)
            return new UserUpdateResult(UserOperationStatus.ValidationFailed);

        string? name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim();
        string? email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

        if (name == null && email == null)
            return new UserUpdateResult(UserOperationStatus.ValidationFailed);

        User? user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return new UserUpdateResult(UserOperationStatus.NotFound);

        if (name != null)
        {
            if (name.Length < 2 || name.Length > 100)
                return new UserUpdateResult(UserOperationStatus.ValidationFailed);

            bool nameExists = await _context.Users.AnyAsync(u => u.Name == name && u.Id != id);
            if (nameExists)
                return new UserUpdateResult(UserOperationStatus.Conflict);

            user.Name = name;
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
            Email = user.Email,
            Level = user.Level
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
}
