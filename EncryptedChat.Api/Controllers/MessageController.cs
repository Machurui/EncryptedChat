using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EncryptedChat.Models;
using EncryptedChat.Services;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MessageController(IMessageService messageService, ITeamService teamService, IRealtimeService realtimeService, IRateLimitService rateLimitService) : ControllerBase
{
    private readonly IMessageService _messageService = messageService;
    private readonly ITeamService _teamService = teamService;
    private readonly IRealtimeService _realtimeService = realtimeService;
    private readonly IRateLimitService _rateLimitService = rateLimitService;

    private string? GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("team/{teamId}")]
    public async Task<IActionResult> GetMessagesByTeam(Guid teamId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return Forbid();

        IReadOnlyList<MessageDTOPublic>? messages = await _messageService.GetAllByTeamAsync(userId, teamId, page, pageSize);
        if (messages is null)
            return NotFound();

        return Ok(messages);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMessage(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        MessageDTOPublic? message = await _messageService.GetByIdAsync(id, userId);
        if (message is null)
            return NotFound();

        return Ok(message);
    }

    [HttpPost]
    public async Task<IActionResult> PostMessage([FromBody] MessageCreateDTO dto)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var rateCheck = _rateLimitService.CheckAndRecord(userId);
        if (!rateCheck.Allowed)
        {
            Response.Headers["Retry-After"] = Math.Ceiling(rateCheck.RetryAfterMs / 1000.0).ToString();
            return StatusCode(429, new { error = "RateLimited", retryAfterMs = rateCheck.RetryAfterMs });
        }

        bool isMember = await _teamService.IsMemberAsync(userId, dto.Team);
        if (!isMember)
            return Forbid();

        MessageDTO messageDto = new()
        {
            Team = dto.Team,
            EncryptedText = dto.EncryptedText,
            Iv = dto.Iv,
            Signature = dto.Signature,
            KeyGeneration = dto.KeyGeneration
        };

        MessageDTOPublic? message = await _messageService.CreateAsync(messageDto, userId);
        if (message is null)
            return BadRequest(new { Message = "Invalid request" });

        // Broadcast to team members
        await _realtimeService.BroadcastMessageAsync(dto.Team, message);

        // Update last message for sidebar. The server cannot read the
        // plaintext anymore, so it ships the envelope; clients decrypt
        // locally and render their own preview. Empty preview is fine —
        // the timestamp + sender name are still useful.
        IReadOnlyList<string> memberIds = await _teamService.GetMemberUserIdsAsync(dto.Team);
        if (memberIds.Count > 0)
        {
            await _realtimeService.BroadcastTeamLastMessageAsync(
                dto.Team, memberIds, string.Empty, message.Date, message.Sender?.Name);
        }

        return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, message);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        MessageDTOPublic? deleted = await _messageService.DeleteAsync(id, userId);
        if (deleted is null)
            return NotFound();

        return NoContent();
    }
}
