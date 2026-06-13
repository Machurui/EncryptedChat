using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EncryptedChat.Models;

public class UserGifVault
{
    [Key]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    // Vault AES key wrapped to the user's identity ECDH public key (base64).
    [Required]
    [MaxLength(512)]
    public string WrappedKey { get; set; } = string.Empty;

    // AES-GCM IV for Blob (base64; 12 bytes -> 16 chars).
    [Required]
    [MaxLength(24)]
    public string Iv { get; set; } = string.Empty;

    // AES-GCM ciphertext+tag of the vault JSON (base64). Opaque to the server.
    [Required]
    public string Blob { get; set; } = string.Empty;

    // Optimistic-concurrency token; incremented on every successful write.
    public int Revision { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
