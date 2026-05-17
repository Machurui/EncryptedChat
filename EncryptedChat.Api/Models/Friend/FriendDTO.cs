namespace EncryptedChat.Models;

public class FriendDTO
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Handle { get; set; }
    public int Level { get; set; }
    public string NameColor { get; set; } = "#FFFFFF";
    public string? ProfileImageUrl { get; set; }
    public DateTime FriendsSince { get; set; }
    public string Status { get; set; } = "offline";
    public string? StatusMessage { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

public class FriendRequestDTO
{
    public Guid RequestId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Handle { get; set; }
    public int Level { get; set; }
    public string NameColor { get; set; } = "#FFFFFF";
    public string? ProfileImageUrl { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsIncoming { get; set; }
}
