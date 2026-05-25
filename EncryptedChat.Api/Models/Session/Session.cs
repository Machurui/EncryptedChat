namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Session
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DeviceInfo { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string DeviceKind { get; set; } = "web";

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    // Links this session to the refresh token currently keeping it alive.
    // Rotated on every RefreshAsync; nulled (SetNull) if the refresh token
    // row is deleted. A session is only "active" if this FK points to a
    // non-revoked, non-expired RefreshToken — that is the real auth ground
    // truth, not the session's own ExpiresAt clock.
    public Guid? CurrentRefreshTokenId { get; set; }

    [ForeignKey("CurrentRefreshTokenId")]
    public RefreshToken? CurrentRefreshToken { get; set; }
}
