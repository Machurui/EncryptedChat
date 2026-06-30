using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EncryptedChat.Models;
using EncryptedChat.Services;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[Route("api/team/{teamId}/pins")]
[ApiController]
[Authorize]
public class PinnedMessageController(IPinnedMessageService pinnedService, ITeamService teamService) : ControllerBase
{
    private readonly IPinnedMessageService _pinnedService = pinnedService;
    private readonly ITeamService _teamService = teamService;

    private string? GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetPinnedMessages(Guid teamId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return Forbid();

        List<PinnedMessageDTO> pins = await _pinnedService.GetPinnedMessagesAsync(teamId, userId);
        return Ok(pins);
    }

    [HttpPost("{messageId}")]
    public async Task<IActionResult> PinMessage(Guid teamId, Guid messageId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return Forbid();

        PinnedMessageDTO? pin = await _pinnedService.PinMessageAsync(teamId, messageId, userId);
        if (pin == null)
            return BadRequest(new { Message = "Unable to pin message. Already pinned or not found." });

        return CreatedAtAction(nameof(GetPinnedMessages), new { teamId }, pin);
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> UnpinMessage(Guid teamId, Guid messageId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return Forbid();

        bool success = await _pinnedService.UnpinMessageAsync(teamId, messageId, userId);
        if (!success)
            return NotFound(new { Message = "Pin not found or access denied." });

        return NoContent();
    }
}
