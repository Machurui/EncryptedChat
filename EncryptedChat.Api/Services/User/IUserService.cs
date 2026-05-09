using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IUserService
{
    Task<UserProfileDTO?> GetOwnProfileAsync(string id);
    Task<UserDTOPublic?> GetUserAsync(string userId, string requesterId);
    Task<IReadOnlyList<UserTeamDTO>> GetUserTeamsAsync(string userId, string requesterId, int page = 1, int pageSize = 20);
    Task<IReadOnlyList<UserDTOPublic>> SearchUsersAsync(string query, string requesterId, int limit = 10);
    Task<UserUpdateResult> UpdateAsync(string id, string requesterId, UserUpdateDTO dto);
    Task<UserDeleteResult> DeleteAsync(string id, string requesterId);
}

public enum UserOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    ValidationFailed
}

public sealed record UserUpdateResult(UserOperationStatus Status, UserProfileDTO? User = null);

public sealed record UserDeleteResult(UserOperationStatus Status);
