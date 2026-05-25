using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IFriendService
{
    Task<IReadOnlyList<FriendDTO>> GetFriendsAsync(string userId);
    Task<IReadOnlyList<FriendRequestDTO>> GetPendingRequestsAsync(string userId);
    Task<FriendRequestDTO?> SendRequestAsync(string requesterId, string addresseeId);
    Task<(bool Success, string? RequesterId, FriendDTO? AccepterAsFriend)> AcceptRequestAsync(string userId, Guid requestId);
    Task<(bool Success, string? OtherUserId)> RejectRequestAsync(string userId, Guid requestId);
    Task<(bool Success, string? RemovedFriendId, Guid? DeletedDmId)> RemoveFriendAsync(string userId, string friendId);
    Task<bool> AreFriendsAsync(string userId1, string userId2);
    Task<IReadOnlyList<UserDTOPublic>> SearchFriendsAsync(string userId, string query, int limit = 10);
    Task<IReadOnlyList<string>> GetPendingRequestUserIdsAsync(string userId);
}
