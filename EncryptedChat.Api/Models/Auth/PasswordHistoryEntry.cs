using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class PasswordHistoryEntry
{
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public User? User { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
