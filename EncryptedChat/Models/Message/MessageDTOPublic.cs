namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;


public class MessageDTOPublic
{
    public int Id { get; set; }

    [Required]
    public string? Text { get; set; }

    [Required]
    public UserDTOPublic? Sender { get; set; }

    [Required]
    public TeamDTOPublic? Team { get; set; }

    [Required]
    public DateTime Date { get; set; }
}