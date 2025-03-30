namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;


public class Message
{
    public int Id { get; set; }

    [Required]
    public string? Text { get; set; }

    [Required]
    public int SenderId { get; set; }

    [Required]
    public int ReceiverId { get; set; }

    [Required]
    public DateTime Date { get; set; }
}
