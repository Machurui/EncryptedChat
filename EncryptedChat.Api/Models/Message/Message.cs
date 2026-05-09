using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EncryptedChat.Models;

public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string EncryptedText { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string Iv { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Signature { get; set; } = string.Empty;

    [Required]
    public User? Sender { get; set; }

    [Required]
    public Team? Team { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public ICollection<Attachment> Attachments { get; set; } = [];
}
