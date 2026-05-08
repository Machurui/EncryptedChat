namespace EncryptedChat.Models;

public class MemberDTOPublic
{
    public UserDTOPublic? User { get; set; }

    public string Role { get; set; } = string.Empty;
}
