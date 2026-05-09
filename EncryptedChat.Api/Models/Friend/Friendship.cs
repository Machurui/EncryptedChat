namespace EncryptedChat.Models;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RequesterId { get; set; } = string.Empty;
    public User? Requester { get; set; }

    public string AddresseeId { get; set; } = string.Empty;
    public User? Addressee { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
}

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}
