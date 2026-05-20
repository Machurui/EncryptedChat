namespace EncryptedChat.Models;

public class UserTeamDTO
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Glyph { get; set; } = "◆";

    public string Color { get; set; } = "oklch(0.65 0.16 165)";

    public string MessageLifetime { get; set; } = "off";

    public bool IsDirect { get; set; } = false;

    public string? LastMessagePreview { get; set; }

    public DateTime? LastMessageTime { get; set; }

    public string? LastMessageSenderName { get; set; }
}
