using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EncryptedChat.Services;

public class SessionService(EncryptedChatContext context) : ISessionService
{
    private readonly EncryptedChatContext _context = context;
    private const int SessionExpirationDays = 30;

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public async Task<Session> CreateSessionAsync(string userId, string token, string deviceInfo, string deviceKind, string? ipAddress)
    {
        var tokenHash = HashToken(token);

        var session = new Session
        {
            UserId = userId,
            TokenHash = tokenHash,
            DeviceInfo = deviceInfo,
            DeviceKind = deviceKind,
            IpAddress = MaskIpAddress(ipAddress),
            ExpiresAt = DateTime.UtcNow.AddDays(SessionExpirationDays)
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<SessionListDTO> GetUserSessionsAsync(string userId, string? currentTokenHash)
    {
        var sessions = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsRevoked && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(s => s.LastActiveAt)
            .Select(s => new SessionDTO(
                s.Id,
                s.DeviceInfo,
                s.DeviceKind,
                s.Location,
                s.IpAddress,
                s.CreatedAt,
                s.LastActiveAt,
                currentTokenHash != null && s.TokenHash == currentTokenHash
            ))
            .ToListAsync();

        return new SessionListDTO(sessions.Count, sessions);
    }

    public async Task<bool> RevokeSessionAsync(string userId, Guid sessionId)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && !s.IsRevoked);

        if (session == null)
            return false;

        session.IsRevoked = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> RevokeAllOtherSessionsAsync(string userId, string? currentTokenHash)
    {
        var sessions = await _context.Sessions
            .Where(s => s.UserId == userId && !s.IsRevoked && (currentTokenHash == null || s.TokenHash != currentTokenHash))
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
        return sessions.Count;
    }

    public async Task<bool> UpdateLastActiveAsync(string tokenHash)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && !s.IsRevoked);

        if (session == null)
            return false;

        session.LastActiveAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsSessionValidAsync(string tokenHash)
    {
        return await _context.Sessions
            .AnyAsync(s => s.TokenHash == tokenHash && !s.IsRevoked && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow));
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = await _context.Sessions
            .Where(s => s.IsRevoked || (s.ExpiresAt != null && s.ExpiresAt < DateTime.UtcNow))
            .ToListAsync();

        _context.Sessions.RemoveRange(expiredSessions);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Session>> GetAllUserSessionsDebugAsync(string userId)
    {
        return await _context.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    private static string? MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return null;

        if (ipAddress.Contains('.'))
        {
            var parts = ipAddress.Split('.');
            if (parts.Length == 4)
                return $"{parts[0]}.{parts[1]}.•.•";
        }
        else if (ipAddress.Contains(':'))
        {
            var parts = ipAddress.Split(':');
            if (parts.Length >= 4)
                return $"{parts[0]}:{parts[1]}:•:•";
        }

        return ipAddress;
    }
}
