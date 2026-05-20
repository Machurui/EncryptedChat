using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using EncryptedChat.Services;
using EncryptedChat.Models;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SecurityController(
    IAuthService authService,
    ISessionService sessionService,
    IRecoveryService recoveryService) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly ISessionService _sessionService = sessionService;
    private readonly IRecoveryService _recoveryService = recoveryService;

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private string? GetCurrentTokenHash()
    {
        var token = HttpContext.Request.Cookies["ec.accessToken"];
        if (string.IsNullOrEmpty(token))
        {
            var authHeader = HttpContext.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = authHeader["Bearer ".Length..].Trim();
        }
        return string.IsNullOrEmpty(token) ? null : SessionService.HashToken(token);
    }

    // POST: api/Security/password
    [HttpPost("password")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (dto.NewPassword != dto.ConfirmPassword)
            return BadRequest(new { Message = "Passwords do not match" });

        var result = await _authService.ChangePasswordAsync(userId, dto);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { Message = "Failed to change password", Errors = errors });
        }

        return Ok(new { Message = "Password changed successfully" });
    }

    // GET: api/Security/password/info
    [HttpGet("password/info")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetPasswordInfo()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var changedAt = await _authService.GetPasswordChangedAtAsync(userId);

        return Ok(new { ChangedAt = changedAt });
    }

    // POST: api/Security/recovery
    [HttpPost("recovery")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetRecoveryPhrase([FromBody] RecoveryPhraseRequestDTO dto)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _recoveryService.GetRecoveryPhraseAsync(userId, dto.Password);
        if (result == null)
            return Unauthorized(new { Message = "Invalid password" });

        return Ok(result);
    }

    // POST: api/Security/recovery/regenerate
    [HttpPost("recovery/regenerate")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RegenerateRecoveryPhrase([FromBody] RecoveryPhraseRequestDTO dto)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _recoveryService.GenerateRecoveryPhraseAsync(userId);
        if (result == null)
            return BadRequest(new { Message = "Failed to generate recovery phrase" });

        return Ok(result);
    }

    // GET: api/Security/recovery/info
    [HttpGet("recovery/info")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetRecoveryInfo()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var lastViewed = await _recoveryService.GetLastViewedAsync(userId);

        return Ok(new { LastViewed = lastViewed });
    }

    // GET: api/Security/sessions
    [HttpGet("sessions")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tokenHash = GetCurrentTokenHash();
        var sessions = await _sessionService.GetUserSessionsAsync(userId, tokenHash);

        return Ok(sessions);
    }

    // DELETE: api/Security/sessions/{sessionId}
    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _sessionService.RevokeSessionAsync(userId, sessionId);
        if (!success)
            return NotFound(new { Message = "Session not found" });

        return NoContent();
    }

    // GET: api/Security/sessions/debug
    [HttpGet("sessions/debug")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> DebugSessions()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tokenHash = GetCurrentTokenHash();
        var allSessions = await _sessionService.GetAllUserSessionsDebugAsync(userId);

        return Ok(new {
            UserId = userId,
            CurrentTokenHash = tokenHash?.Substring(0, Math.Min(10, tokenHash?.Length ?? 0)) + "...",
            HasCookie = !string.IsNullOrEmpty(HttpContext.Request.Cookies["ec.accessToken"]),
            TotalSessions = allSessions.Count,
            Sessions = allSessions.Select(s => new {
                s.Id,
                TokenHashPrefix = s.TokenHash.Substring(0, Math.Min(10, s.TokenHash.Length)) + "...",
                s.DeviceInfo,
                s.IsRevoked,
                s.CreatedAt,
                MatchesCurrent = s.TokenHash == tokenHash
            })
        });
    }

    // DELETE: api/Security/sessions
    [HttpDelete("sessions")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RevokeAllOtherSessions()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tokenHash = GetCurrentTokenHash();
        var count = await _sessionService.RevokeAllOtherSessionsAsync(userId, tokenHash);

        return Ok(new { RevokedCount = count, Message = $"Revoked {count} session(s)" });
    }
}
