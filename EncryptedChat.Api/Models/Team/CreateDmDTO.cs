namespace EncryptedChat.Models;

public class CreateDmDTO
{
    public string MyWrappedKey { get; set; } = string.Empty;
    public string FriendWrappedKey { get; set; } = string.Empty;
}
