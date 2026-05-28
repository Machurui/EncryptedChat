using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class Team
{
    public static readonly string[] ValidGlyphs = ["◆", "◈", "✦", "⌘", "◇", "⬢"];
    public static readonly string[] ValidColors =
    [
        "oklch(0.65 0.16 230)",
        "oklch(0.65 0.15 165)",
        "oklch(0.65 0.16 305)",
        "oklch(0.66 0.16 30)",
        "oklch(0.70 0.15 75)",
        "oklch(0.62 0.16 195)"
    ];
    public static readonly string[] ValidMessageLifetimes = ["off", "24h", "7d", "30d"];

    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string Secret { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Glyph { get; set; } = "◆";

    [Required]
    [MaxLength(50)]
    public string Color { get; set; } = "oklch(0.65 0.16 165)";

    [Required]
    [MaxLength(10)]
    public string MessageLifetime { get; set; } = "off";

    [MaxLength(64)]
    public string? OwnBubbleColor { get; set; }

    public bool IsDirect { get; set; } = false;

    [Required]
    public ICollection<Member> Members { get; set; } = [];

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
