using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class FriendService(EncryptedChatContext context, IPresenceService presenceService) : IFriendService
{
    private readonly EncryptedChatContext _context = context;
    private readonly IPresenceService _presenceService = presenceService;

    public async Task<IReadOnlyList<FriendDTO>> GetFriendsAsync(string userId)
    {
        List<Friendship> friendships = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return [.. friendships.Select(f =>
        {
            User? friend = f.RequesterId == userId ? f.Addressee : f.Requester;
            string rawStatus = string.IsNullOrEmpty(friend!.Status) ? "online" : friend.Status;

            // Check if user is actually connected via SignalR
            bool isConnected = _presenceService.IsOnline(friend.Id);

            // If not connected, show as offline regardless of profile status
            // If connected but invisible, show as offline
            string displayStatus = !isConnected ? "offline" : (rawStatus == "invisible" ? "offline" : rawStatus);

            return new FriendDTO
            {
                UserId = friend.Id,
                Name = friend.Name,
                Handle = friend.Handle,
                Level = friend.Level,
                NameColor = friend.NameColor,
                ProfileImageUrl = friend.ProfileImageUrl,
                FriendsSince = f.AcceptedAt ?? f.CreatedAt,
                Status = displayStatus,
                StatusMessage = (!isConnected || rawStatus == "invisible") ? null : friend.StatusMessage,
                LastSeenAt = friend.LastSeenAt
            };
        })];
    }

    public async Task<IReadOnlyList<FriendDTO>> SearchFriendsAsync(string userId, string? q, int limit)
    {
        // Friends are a small, already-decrypted set, so a partial in-memory filter
        // is fine here (unlike the all-users search, which must use the exact blind index).
        IReadOnlyList<FriendDTO> friends = await GetFriendsAsync(userId);
        string term = (q ?? string.Empty).Trim();

        IEnumerable<FriendDTO> matches = string.IsNullOrEmpty(term)
            ? friends
            : friends.Where(f =>
                (f.Name ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (f.Handle ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));

        return [.. matches.Take(Math.Clamp(limit, 1, 100))];
    }

    public async Task<IReadOnlyList<FriendRequestDTO>> GetPendingRequestsAsync(string userId)
    {
        List<Friendship> requests = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return [.. requests.Select(f =>
        {
            bool isIncoming = f.AddresseeId == userId;
            User? otherUser = isIncoming ? f.Requester : f.Addressee;

            return new FriendRequestDTO
            {
                RequestId = f.Id,
                UserId = otherUser!.Id,
                Name = otherUser.Name,
                Handle = otherUser.Handle,
                Level = otherUser.Level,
                NameColor = otherUser.NameColor,
                ProfileImageUrl = otherUser.ProfileImageUrl,
                SentAt = f.CreatedAt,
                IsIncoming = isIncoming
            };
        })];
    }

    public async Task<FriendRequestDTO?> SendRequestAsync(string requesterId, string addresseeId)
    {
        if (requesterId == addresseeId)
            return null;

        User? requester = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == requesterId);
        if (requester == null)
            return null;

        bool addresseeExists = await _context.Users.AnyAsync(u => u.Id == addresseeId);
        if (!addresseeExists)
            return null;

        bool existingFriendship = await _context.Friendships
            .AnyAsync(f =>
                (f.RequesterId == requesterId && f.AddresseeId == addresseeId) ||
                (f.RequesterId == addresseeId && f.AddresseeId == requesterId));

        if (existingFriendship)
            return null;

        Friendship friendship = new()
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        return new FriendRequestDTO
        {
            RequestId = friendship.Id,
            UserId = requester.Id,
            Name = requester.Name,
            Handle = requester.Handle,
            Level = requester.Level,
            NameColor = requester.NameColor,
            ProfileImageUrl = requester.ProfileImageUrl,
            SentAt = friendship.CreatedAt,
            IsIncoming = true
        };
    }

    public async Task<(bool Success, string? RequesterId, FriendDTO? AccepterAsFriend)> AcceptRequestAsync(string userId, Guid requestId)
    {
        Friendship? request = await _context.Friendships
            .Include(f => f.Addressee)
            .FirstOrDefaultAsync(f => f.Id == requestId &&
                                      f.AddresseeId == userId &&
                                      f.Status == FriendshipStatus.Pending);

        if (request == null)
            return (false, null, null);

        request.Status = FriendshipStatus.Accepted;
        request.AcceptedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        User accepter = request.Addressee!;
        string displayStatus = accepter.Status == "invisible" ? "offline" : accepter.Status;

        FriendDTO friendDto = new()
        {
            UserId = accepter.Id,
            Name = accepter.Name,
            Handle = accepter.Handle,
            Level = accepter.Level,
            NameColor = accepter.NameColor,
            ProfileImageUrl = accepter.ProfileImageUrl,
            FriendsSince = request.AcceptedAt ?? DateTime.UtcNow,
            Status = displayStatus,
            StatusMessage = accepter.Status == "invisible" ? null : accepter.StatusMessage
        };

        return (true, request.RequesterId, friendDto);
    }

    public async Task<(bool Success, string? OtherUserId)> RejectRequestAsync(string userId, Guid requestId)
    {
        Friendship? request = await _context.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId &&
                                      (f.AddresseeId == userId || f.RequesterId == userId) &&
                                      f.Status == FriendshipStatus.Pending);

        if (request == null)
            return (false, null);

        string otherUserId = request.RequesterId == userId ? request.AddresseeId : request.RequesterId;

        _context.Friendships.Remove(request);
        await _context.SaveChangesAsync();
        return (true, otherUserId);
    }

    public async Task<(bool Success, string? RemovedFriendId, Guid? DeletedDmId)> RemoveFriendAsync(string userId, string friendId)
    {
        Friendship? friendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == userId && f.AddresseeId == friendId) ||
                 (f.RequesterId == friendId && f.AddresseeId == userId)));

        if (friendship == null)
            return (false, null, null);

        Guid? deletedDmId = null;

        Team? dm = await _context.Teams
            .Include(t => t.Members)
            .Where(t => t.IsDirect && t.Members.Count == 2)
            .Where(t => t.Members.Any(m => m.UserId == userId) && t.Members.Any(m => m.UserId == friendId))
            .FirstOrDefaultAsync();

        if (dm != null)
        {
            deletedDmId = dm.Id;

            List<Message> messages = await _context.Messages
                .Where(m => m.Team != null && m.Team.Id == dm.Id)
                .ToListAsync();

            _context.Messages.RemoveRange(messages);

            List<Member> members = await _context.Members.Where(m => m.TeamId == dm.Id).ToListAsync();
            _context.Members.RemoveRange(members);

            _context.Teams.Remove(dm);
        }

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
        return (true, friendId, deletedDmId);
    }

    public async Task<bool> AreFriendsAsync(string userId1, string userId2)
    {
        return await _context.Friendships
            .AnyAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                 (f.RequesterId == userId2 && f.AddresseeId == userId1)));
    }

    public async Task<IReadOnlyList<string>> GetPendingRequestUserIdsAsync(string userId)
    {
        List<string> userIds = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        return userIds;
    }
}
