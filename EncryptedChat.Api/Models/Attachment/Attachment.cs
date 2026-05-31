using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EncryptedChat.Models;

public class Attachment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MessageId { get; set; }

    [Required]
    public Message Message { get; set; } = null!;

    [Required]
    [MaxLength(512)]
    public string EncryptedFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string FileNameIv { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    public long Size { get; set; }

    [Required]
    [MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string FileIv { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Signature { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int KeyGeneration { get; set; } = 1;
}
