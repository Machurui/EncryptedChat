using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EncryptedChat.Models;

public class TeamInvite
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public Team? Team { get; set; }

    // 32 random bytes, base64url. Opaque — the only thing the QR/link carries.
    [Required]
    [MaxLength(64)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
