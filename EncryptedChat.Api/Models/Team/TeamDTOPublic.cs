namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

// Affichage vers le client
public class TeamDTOPublic
{
    public Guid Id { get; set; }

    public ICollection<MemberDTOPublic>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

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

    public bool IsDirect { get; set; } = false;
}
