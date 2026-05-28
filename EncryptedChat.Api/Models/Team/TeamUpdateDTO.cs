using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class TeamUpdateDTO
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(10)]
    public string? Glyph { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    [MaxLength(10)]
    public string? MessageLifetime { get; set; }

    public string? OwnBubbleColor { get; set; }
}
