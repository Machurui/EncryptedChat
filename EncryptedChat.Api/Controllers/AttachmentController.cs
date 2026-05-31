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

    // Clients POST the already-ciphered blob + envelope as multipart/form-data.
    // The server treats every field as opaque (it can't decrypt) and
    // only validates size, declared MIME, and team membership.
    [HttpPost]
    [EnableRateLimiting("AttachmentUpload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] Guid messageId,
        [FromForm] string encryptedFileName,
        [FromForm] string fileNameIv,
        [FromForm] string fileIv,
        [FromForm] string signature,
        [FromForm] string mimeType,
        [FromForm] int keyGeneration)
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

        AttachmentUploadDTO upload = new()
        {
            EncryptedContent = ms.ToArray(),
            EncryptedFileName = encryptedFileName ?? string.Empty,
            FileNameIv = fileNameIv ?? string.Empty,
            FileIv = fileIv ?? string.Empty,
            Signature = signature ?? string.Empty,
            MimeType = mimeType ?? string.Empty,
            KeyGeneration = keyGeneration
        };

        (AttachmentDTOPublic? attachment, string? error, bool isForbidden) =
            await _attachmentService.CreateAsync(messageId, upload, userId);

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

    // Returns the raw ciphertext as application/octet-stream alongside
    // headers describing the envelope. Clients decrypt locally before
    // showing anything to the user.
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        AttachmentDownloadDTO? result = await _attachmentService.DownloadAsync(id, userId);
        if (result == null)
            return NotFound();

        Response.Headers["X-Encrypted-FileName"] = result.EncryptedFileName;
        Response.Headers["X-FileName-Iv"] = result.FileNameIv;
        Response.Headers["X-File-Iv"] = result.FileIv;
        Response.Headers["X-Signature"] = result.Signature;
        Response.Headers["X-Key-Generation"] = result.KeyGeneration.ToString();
        Response.Headers["X-Mime-Type"] = result.MimeType;

        return File(result.EncryptedContent, "application/octet-stream");
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
