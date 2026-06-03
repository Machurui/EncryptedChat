using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class Member
{
    // Roles ordered from highest privilege to lowest. Exactly one Owner per
    // team; Owner is the only role that can promote/demote Admins, transfer
    // ownership, and delete the team. Admins manage Members only.
    public const string OwnerRole = "Owner";
    public const string AdminRole = "Admin";
    public const string MemberRole = "Member";

    // "Admin or above" — true for Admin and Owner. Use this for admin-level
    // authorization on a materialized Member instead of comparing Role to
    // AdminRole directly, so the Owner role is never accidentally excluded.
    // Do NOT use inside an EF query expression — it cannot be translated to SQL;
    // there, inline (Role == AdminRole || Role == OwnerRole) as IsAdminAsync does.
    public static bool IsAdminOrAbove(string? role) => role == AdminRole || role == OwnerRole;

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
