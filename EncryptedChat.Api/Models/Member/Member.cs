using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class Member
{
    public const string AdminRole = "Admin";
    public const string MemberRole = "Member";

    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TeamId { get; set; }

    public Team? Team { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public User? User { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = MemberRole;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(16)]
    public string UrlToken { get; set; } = string.Empty;
}
