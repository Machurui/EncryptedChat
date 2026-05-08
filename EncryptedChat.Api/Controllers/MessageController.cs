using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EncryptedChat.Models;
using EncryptedChat.Services;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MessageController(IMessageService messageService, ITeamService teamService) : ControllerBase
{
    private readonly IMessageService _messageService = messageService;
    private readonly ITeamService _teamService = teamService;

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

        IReadOnlyList<MessageDTOPublic>? messages = await _messageService.GetAllByTeamAsync(teamId, page, pageSize);
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

        MessageDTOPublic? message = await _messageService.GetByIdAsync(id);
        if (message is null)
            return NotFound();

        bool isMember = await _teamService.IsMemberAsync(userId, message.TeamId);
        if (!isMember)
            return Forbid();

        return Ok(message);
    }

    [HttpPost]
    public async Task<IActionResult> PostMessage([FromBody] MessageCreateDTO dto)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool isMember = await _teamService.IsMemberAsync(userId, dto.Team);
        if (!isMember)
            return Forbid();

        MessageDTO messageDto = new()
        {
            Text = dto.Text,
            Sender = userId,
            Team = dto.Team
        };

        MessageDTOPublic? message = await _messageService.CreateAsync(messageDto);
        if (message is null)
            return BadRequest(new { Message = "Invalid request" });

        return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, message);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        MessageDTOPublic? message = await _messageService.GetByIdAsync(id);
        if (message is null)
            return NotFound();

        bool isAdmin = await _teamService.IsAdminAsync(userId, message.TeamId);
        bool isOwner = message.Sender?.Id == userId;

        if (!isAdmin && !isOwner)
            return Forbid();

        await _messageService.DeleteAsync(id);
        return NoContent();
    }
}
