namespace EncryptedChat.Models;

public class FriendDTO
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public DateTime FriendsSince { get; set; }
}

public class FriendRequestDTO
{
    public Guid RequestId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsIncoming { get; set; }
}
