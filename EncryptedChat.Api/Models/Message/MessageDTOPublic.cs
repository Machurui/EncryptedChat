using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

// Wire shape returned to clients for every read. The server passes the
// encrypted envelope through verbatim; the client is responsible for
// AES-GCM decryption and ECDSA signature verification under the sender's
// public key + the team key for the given KeyGeneration.
public class MessageDTOPublic
{
    public Guid Id { get; set; }

    [Required]
    public string EncryptedText { get; set; } = string.Empty;

    [Required]
    public string Iv { get; set; } = string.Empty;

    [Required]
    public string Signature { get; set; } = string.Empty;

    [Required]
    public int KeyGeneration { get; set; }

    [Required]
    public MessageSenderDTO? Sender { get; set; }

    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<AttachmentDTOPublic> Attachments { get; set; } = [];
}

public class MessageSenderDTO
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Handle { get; set; }
    public string NameColor { get; set; } = "#FFFFFF";
    public string? ProfileImageUrl { get; set; }
}
