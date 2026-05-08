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
public class AttachmentController(IAttachmentService attachmentService) : ControllerBase
{
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

        (AttachmentDTOPublic? attachment, string? error, bool isForbidden) = await attachmentService.CreateAsync(
            messageId, file.FileName, file.ContentType, ms.ToArray(), userId);

        if (attachment == null)
            return isForbidden ? Forbid() : BadRequest(new { Message = error });

        return CreatedAtAction(nameof(GetMetadata), new { id = attachment.Id }, attachment);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMetadata(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        AttachmentDTOPublic? attachment = await attachmentService.GetByIdAsync(id, userId);
        return attachment == null ? NotFound() : Ok(attachment);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        (byte[] Content, string FileName, string MimeType)? result = await attachmentService.DownloadAsync(id, userId);
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

        return await attachmentService.DeleteAsync(id, userId) ? NoContent() : NotFound();
    }
}
