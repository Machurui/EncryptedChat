namespace EncryptedChat.Models;

public class MemberDTOPublic
{
    public Guid Id { get; set; }

    public UserDTOPublic? User { get; set; }

    public string Role { get; set; } = string.Empty;
}
