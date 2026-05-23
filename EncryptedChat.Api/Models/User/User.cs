namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

public class User : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$")]
    public string? Handle { get; set; }

    [Required]
    public int Level { get; set; } = 0;

    [Required]
    public string Secret { get; set; } = string.Empty;

    [MaxLength(50)]
    public string NameColor { get; set; } = "#FFFFFF";

    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "online";

    [MaxLength(100)]
    public string? StatusMessage { get; set; }

    [MaxLength(10)]
    public string Theme { get; set; } = "dark";

    // Privacy settings
    public bool ReadReceipts { get; set; } = true;
    public bool TypingIndicators { get; set; } = true;

    [MaxLength(20)]
    public string NotificationPreference { get; set; } = "all";

    // Security
    [MaxLength(500)]
    public string? RecoveryPhraseHash { get; set; }

    public DateTime? RecoveryPhraseLastViewed { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public ICollection<Member> Memberships { get; set; } = [];

    public ICollection<Session> Sessions { get; set; } = [];
}
