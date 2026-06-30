using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

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
