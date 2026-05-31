using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

// Sent by the client when posting a new message. The client has already
// AES-GCM-encrypted the plaintext under the team key for the current
// generation and signed the plaintext with the sender's ECDSA private
// key. The server treats every blob as opaque and never decrypts.
public class MessageCreateDTO
{
    [Required]
    public Guid Team { get; set; }

    // Base64(AES-GCM ciphertext || tag). Max length is intentionally
    // generous (~4000 chars plaintext → ~5500 bytes ciphertext → ~7400
    // chars Base64) so the column accommodates emojis / multi-byte text.
    [Required]
    [MaxLength(8192)]
    public string EncryptedText { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string Iv { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Signature { get; set; } = string.Empty;

    [Required]
    public int KeyGeneration { get; set; }
}
