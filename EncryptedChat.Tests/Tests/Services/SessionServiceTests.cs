using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Tests;

public sealed class SessionServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly SessionService _service;
    private const string UserId = "user-1";

    public SessionServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new EncryptedChatContext(options);
        _service = new SessionService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetUserSessions_OnlyIncludesSessionsWithActiveRefreshToken()
    {
        // Active: linked RefreshToken not revoked, not expired
        RefreshToken activeRt = NewRefreshToken(revokedAt: null, expiresAt: DateTime.UtcNow.AddDays(7));
        // Revoked refresh token → its session must NOT appear as active
        RefreshToken revokedRt = NewRefreshToken(revokedAt: DateTime.UtcNow.AddMinutes(-1), expiresAt: DateTime.UtcNow.AddDays(7));
        // Expired refresh token → its session must NOT appear as active
        RefreshToken expiredRt = NewRefreshToken(revokedAt: null, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        _context.RefreshTokens.AddRange(activeRt, revokedRt, expiredRt);

        _context.Sessions.AddRange(
            NewSession("active-device", currentRefreshTokenId: activeRt.Id),
            NewSession("revoked-rt-device", currentRefreshTokenId: revokedRt.Id),
            NewSession("expired-rt-device", currentRefreshTokenId: expiredRt.Id),
            NewSession("orphan-device", currentRefreshTokenId: null) // never linked
        );
        await _context.SaveChangesAsync();

        SessionListDTO result = await _service.GetUserSessionsAsync(UserId, currentTokenHash: null);

        result.TotalCount.Should().Be(1);
        result.Sessions.Should().HaveCount(1)
            .And.OnlyContain(s => s.DeviceInfo == "active-device");
    }

    [Fact]
    public async Task IsSessionValid_ReturnsFalseWhenRefreshTokenRevoked()
    {
        RefreshToken rt = NewRefreshToken(revokedAt: DateTime.UtcNow, expiresAt: DateTime.UtcNow.AddDays(7));
        _context.RefreshTokens.Add(rt);
        Session session = NewSession("device", currentRefreshTokenId: rt.Id);
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        bool valid = await _service.IsSessionValidAsync(session.TokenHash);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task IsSessionValid_ReturnsTrueWhenRefreshTokenActive()
    {
        RefreshToken rt = NewRefreshToken(revokedAt: null, expiresAt: DateTime.UtcNow.AddDays(7));
        _context.RefreshTokens.Add(rt);
        Session session = NewSession("device", currentRefreshTokenId: rt.Id);
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        bool valid = await _service.IsSessionValidAsync(session.TokenHash);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeSession_AlsoRevokesLinkedRefreshToken()
    {
        RefreshToken rt = NewRefreshToken(revokedAt: null, expiresAt: DateTime.UtcNow.AddDays(7));
        _context.RefreshTokens.Add(rt);
        Session session = NewSession("device", currentRefreshTokenId: rt.Id);
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        bool revoked = await _service.RevokeSessionAsync(UserId, session.Id);

        revoked.Should().BeTrue();
        RefreshToken stored = await _context.RefreshTokens.SingleAsync();
        stored.RevokedAt.Should().NotBeNull(
            "revoking a session must also revoke its refresh token, else the device can refresh back in");
    }

    [Fact]
    public async Task RevokeAllOtherSessions_RevokesLinkedRefreshTokensForOtherSessions()
    {
        RefreshToken currentRt = NewRefreshToken(revokedAt: null, expiresAt: DateTime.UtcNow.AddDays(7));
        RefreshToken otherRt = NewRefreshToken(revokedAt: null, expiresAt: DateTime.UtcNow.AddDays(7));
        _context.RefreshTokens.AddRange(currentRt, otherRt);

        Session currentSession = NewSession("current-device", currentRefreshTokenId: currentRt.Id);
        Session otherSession = NewSession("other-device", currentRefreshTokenId: otherRt.Id);
        _context.Sessions.AddRange(currentSession, otherSession);
        await _context.SaveChangesAsync();

        int count = await _service.RevokeAllOtherSessionsAsync(UserId, currentTokenHash: currentSession.TokenHash);

        count.Should().Be(1);
        (await _context.RefreshTokens.SingleAsync(rt => rt.Id == otherRt.Id))
            .RevokedAt.Should().NotBeNull();
        (await _context.RefreshTokens.SingleAsync(rt => rt.Id == currentRt.Id))
            .RevokedAt.Should().BeNull("the current session's refresh token must stay alive");
    }

    private static RefreshToken NewRefreshToken(DateTime? revokedAt, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        Token = Guid.NewGuid().ToString("N"),
        UserId = UserId,
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt
    };

    private static Session NewSession(string deviceInfo, Guid? currentRefreshTokenId) => new()
    {
        UserId = UserId,
        TokenHash = Guid.NewGuid().ToString("N"),
        DeviceInfo = deviceInfo,
        DeviceKind = "web",
        CreatedAt = DateTime.UtcNow.AddMinutes(-3),
        LastActiveAt = DateTime.UtcNow.AddMinutes(-1),
        ExpiresAt = DateTime.UtcNow.AddDays(30),
        CurrentRefreshTokenId = currentRefreshTokenId
    };
}
