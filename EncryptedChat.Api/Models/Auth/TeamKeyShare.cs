using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class TeamKeyShare
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    [Required]
    public string MemberId { get; set; } = string.Empty;
    public User? Member { get; set; }

    public int Generation { get; set; }

    [Required]
    [MaxLength(256)]
    public string WrappedKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
