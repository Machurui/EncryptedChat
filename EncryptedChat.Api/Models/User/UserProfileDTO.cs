namespace EncryptedChat.Models;

public class UserProfileDTO : UserDTOPublic
{
    public string Email { get; set; } = string.Empty;
    public string Theme { get; set; } = "dark";
    public bool ReadReceipts { get; set; } = true;
    public bool TypingIndicators { get; set; } = true;
    public string NotificationPreference { get; set; } = "all";
}
