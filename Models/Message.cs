namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;


public class Message
{
    public int Id { get; set; }

    [Required]
    public string? Text { get; set; }

    [Required]
    public User? Sender { get; set; }

    [Required]
    public Team? Team { get; set; }

    [Required]
    public DateTime Date { get; set; }
}

public class MessageDTOPrivate
{
    public int Id { get; set; }

    [Required]
    public string? Text { get; set; }

    [Required]
    public UserDTOPrivate? Sender { get; set; }

    [Required]
    public TeamDTOPrivate? Team { get; set; }

    [Required]
    public DateTime Date { get; set; }
}

public class MessageDTO
{
    [Required]
    public string? Text { get; set; }

    [Required]
    public UserDTOAdd? Sender { get; set; }

    [Required]
    public TeamDTOPrivate? Team { get; set; }

    [Required]
    public DateTime Date { get; set; }
}
