namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

public class MessageDTO
{
    [Required]
    public string? Text { get; set; }

    [Required]
    public string? Sender { get; set; }

    [Required]
    public int? Team { get; set; }

}