using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EncryptedChat.Models;

// Multipart upload contract for POST /api/Attachment. The [MaxLength] limits
// mirror the Attachment table columns so that an over-long value is rejected
// with 400 by [ApiController] model validation instead of triggering a SQL
// truncation (DbUpdateException -> 500) at insert time. Property names match the
// multipart field names sent by the Blazor client (case-insensitive binding).
public sealed class AttachmentUploadRequest
{
    public IFormFile File { get; set; } = null!;

    [Required]
    public Guid MessageId { get; set; }

    [Required]
    [MaxLength(512)]
    public string EncryptedFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string FileNameIv { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string FileIv { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Signature { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    public int KeyGeneration { get; set; }
}
