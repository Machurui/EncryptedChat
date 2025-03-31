namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Message
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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

public class MessageDTO
{
    [Required]
    public string? Text { get; set; }

    [Required]
    public int? Sender { get; set; }

    [Required]
    public int? Team { get; set; }

}
