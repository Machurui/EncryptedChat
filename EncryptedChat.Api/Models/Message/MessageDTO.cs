using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

// Internal DTO between Controller and Service for write paths. Carries
// the E2E envelope produced by the client. Unused for reads — those go
// through MessageDTOPublic.
public class MessageDTO
{
    [Required]
    public Guid Team { get; set; }

    [Required]
    public string EncryptedText { get; set; } = string.Empty;

    [Required]
    public string Iv { get; set; } = string.Empty;

    [Required]
    public string Signature { get; set; } = string.Empty;

    [Required]
    public int KeyGeneration { get; set; }
}
