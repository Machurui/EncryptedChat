namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

// Utilisé lors de création et MAJ
public class MessageDTO
{
    [Required]
    public string? Text { get; set; } = string.Empty;

    [Required]
    public string? Sender { get; set; }

    [Required]
    public int? Team { get; set; }

}
