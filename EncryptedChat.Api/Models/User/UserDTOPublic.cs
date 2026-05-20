namespace EncryptedChat.Models;

public class UserDTOPublic
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Handle { get; set; }
    public int Level { get; set; }
    public string NameColor { get; set; } = "#FFFFFF";
    public string? ProfileImageUrl { get; set; }
    public string Status { get; set; } = "online";
    public string? StatusMessage { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
