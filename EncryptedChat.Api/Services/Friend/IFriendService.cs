using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IFriendService
{
    Task<IReadOnlyList<FriendDTO>> GetFriendsAsync(string userId);
    Task<IReadOnlyList<FriendRequestDTO>> GetPendingRequestsAsync(string userId);
    Task<bool> SendRequestAsync(string requesterId, string addresseeId);
    Task<bool> AcceptRequestAsync(string userId, Guid requestId);
    Task<bool> RejectRequestAsync(string userId, Guid requestId);
    Task<bool> RemoveFriendAsync(string userId, string friendId);
    Task<bool> AreFriendsAsync(string userId1, string userId2);
    Task<IReadOnlyList<UserDTOPublic>> SearchFriendsAsync(string userId, string query, int limit = 10);
}
