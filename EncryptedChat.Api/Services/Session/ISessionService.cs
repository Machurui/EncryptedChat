using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface ISessionService
{
    Task<Session> CreateSessionAsync(string userId, string token, string deviceInfo, string deviceKind, string? ipAddress, Guid? refreshTokenId = null);
    Task<SessionListDTO> GetUserSessionsAsync(string userId, string? currentTokenHash);
    Task<bool> RevokeSessionAsync(string userId, Guid sessionId);
    Task<int> RevokeAllOtherSessionsAsync(string userId, string? currentTokenHash);
    Task<int> RevokeAllSessionsAsync(string userId);
    Task<bool> UpdateLastActiveAsync(string tokenHash);
    Task<bool> IsSessionValidAsync(string tokenHash);
    Task CleanupExpiredSessionsAsync();
    Task<List<Session>> GetAllUserSessionsDebugAsync(string userId);
}
