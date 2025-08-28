namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

// Affichage vers le client
public class MessageDTOPublic
{
    public int Id { get; set; }

    [Required]
    public string? Text { get; set; } = string.Empty;

    [Required]
    public UserDTOPublic? Sender { get; set; }

    [Required]
    public TeamDTOPublic? Team { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;
}