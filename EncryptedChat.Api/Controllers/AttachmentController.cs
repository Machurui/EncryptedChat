using System.Security.Claims;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EncryptedChat.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "User")]
[RequestSizeLimit(26_214_400)]
public class AttachmentController(
    IAttachmentService attachmentService,
    IRealtimeService realtimeService) : ControllerBase
{
    private readonly IAttachmentService _attachmentService = attachmentService;
    private readonly IRealtimeService _realtimeService = realtimeService;

    private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost]
    [EnableRateLimiting("AttachmentUpload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] Guid messageId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { Message = "Aucun fichier fourni" });

        if (file.Length > 26_214_400)
            return BadRequest(new { Message = "Fichier trop volumineux (max 25 Mo)" });

        using MemoryStream ms = new();
        await file.CopyToAsync(ms);

        (AttachmentDTOPublic? attachment, string? error, bool isForbidden) = await _attachmentService.CreateAsync(
            messageId, file.FileName, file.ContentType, ms.ToArray(), userId);

        if (attachment == null)
            return isForbidden ? Forbid() : BadRequest(new { Message = error });

        // Broadcast attachment to team
        Guid? teamId = await _attachmentService.GetTeamIdForMessageAsync(messageId);
        if (teamId.HasValue)
        {
            await _realtimeService.BroadcastAttachmentAddedAsync(teamId.Value, messageId, attachment);
        }

        return CreatedAtAction(nameof(GetMetadata), new { id = attachment.Id }, attachment);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMetadata(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        AttachmentDTOPublic? attachment = await _attachmentService.GetByIdAsync(id, userId);
        return attachment == null ? NotFound() : Ok(attachment);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        (byte[] Content, string FileName, string MimeType)? result = await _attachmentService.DownloadAsync(id, userId);
        if (result == null)
            return NotFound();

        (byte[] content, string fileName, string mimeType) = result.Value;
        return File(content, mimeType, fileName);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        return await _attachmentService.DeleteAsync(id, userId) ? NoContent() : NotFound();
    }
}
