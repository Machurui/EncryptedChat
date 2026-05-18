using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class PinnedMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    [Required]
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;

    [Required]
    public string PinnedById { get; set; } = string.Empty;
    public User PinnedBy { get; set; } = null!;

    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}
