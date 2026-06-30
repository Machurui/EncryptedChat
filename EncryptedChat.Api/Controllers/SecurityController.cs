using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using EncryptedChat.Services;
using EncryptedChat.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

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
        string? token = HttpContext.Request.Cookies["ec.accessToken"];
        if (string.IsNullOrEmpty(token))
        {
            string authHeader = HttpContext.Request.Headers.Authorization.ToString();
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
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (dto.NewPassword != dto.ConfirmPassword)
            return BadRequest(new { Message = "Passwords do not match" });

        IdentityResult result = await _authService.ChangePasswordAsync(userId, dto);

        if (!result.Succeeded)
        {
            List<string> errors = [.. result.Errors.Select(e => e.Description)];
            return BadRequest(new { Message = "Failed to change password", Errors = errors });
        }

        return Ok(new { Message = "Password changed successfully" });
    }

    // GET: api/Security/password/info
    [HttpGet("password/info")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetPasswordInfo()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        DateTime? changedAt = await _authService.GetPasswordChangedAtAsync(userId);

        return Ok(new { ChangedAt = changedAt });
    }

    // POST: api/Security/recovery/regenerate
    [HttpPost("recovery/regenerate")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RegenerateRecoveryPhrase([FromBody] RecoveryPhraseRequestDTO dto)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Rotating the recovery phrase invalidates the old one, so require the
        // caller to re-prove their password — an authenticated session alone is
        // not enough for this account-recovery–sensitive action.
        if (!await _authService.VerifyPasswordAsync(userId, dto.Password))
            return BadRequest(new { Message = "Invalid password" });

        RecoveryPhraseDTO? result = await _recoveryService.GenerateRecoveryPhraseAsync(userId);
        if (result == null)
            return BadRequest(new { Message = "Failed to generate recovery phrase" });

        return Ok(result);
    }

    // GET: api/Security/recovery/info
    [HttpGet("recovery/info")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetRecoveryInfo()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        DateTime? lastViewed = await _recoveryService.GetLastViewedAsync(userId);

        return Ok(new { LastViewed = lastViewed });
    }

    // GET: api/Security/sessions
    [HttpGet("sessions")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetSessions()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        string? tokenHash = GetCurrentTokenHash();
        SessionListDTO sessions = await _sessionService.GetUserSessionsAsync(userId, tokenHash);

        return Ok(sessions);
    }

    // DELETE: api/Security/sessions/{sessionId}
    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        bool success = await _sessionService.RevokeSessionAsync(userId, sessionId);
        if (!success)
            return NotFound(new { Message = "Session not found" });

        return NoContent();
    }

    // DELETE: api/Security/sessions
    [HttpDelete("sessions")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RevokeAllOtherSessions()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        string? tokenHash = GetCurrentTokenHash();
        int count = await _sessionService.RevokeAllOtherSessionsAsync(userId, tokenHash);

        return Ok(new { RevokedCount = count, Message = $"Revoked {count} session(s)" });
    }
}
