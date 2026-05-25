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
        var friendships = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return friendships.Select(f =>
        {
            var friend = f.RequesterId == userId ? f.Addressee : f.Requester;
            var rawStatus = string.IsNullOrEmpty(friend!.Status) ? "online" : friend.Status;

            // Check if user is actually connected via SignalR
            var isConnected = _presenceService.IsOnline(friend.Id);

            // If not connected, show as offline regardless of profile status
            // If connected but invisible, show as offline
            var displayStatus = !isConnected ? "offline" : (rawStatus == "invisible" ? "offline" : rawStatus);

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
        }).ToList();
    }

    public async Task<IReadOnlyList<FriendRequestDTO>> GetPendingRequestsAsync(string userId)
    {
        var requests = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .ToListAsync();

        return requests.Select(f =>
        {
            bool isIncoming = f.AddresseeId == userId;
            var otherUser = isIncoming ? f.Requester : f.Addressee;
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
        }).ToList();
    }

    public async Task<FriendRequestDTO?> SendRequestAsync(string requesterId, string addresseeId)
    {
        if (requesterId == addresseeId)
            return null;

        var requester = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == requesterId);
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

        var friendship = new Friendship
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
        var request = await _context.Friendships
            .Include(f => f.Addressee)
            .FirstOrDefaultAsync(f => f.Id == requestId &&
                                      f.AddresseeId == userId &&
                                      f.Status == FriendshipStatus.Pending);

        if (request == null)
            return (false, null, null);

        request.Status = FriendshipStatus.Accepted;
        request.AcceptedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var accepter = request.Addressee!;
        var displayStatus = accepter.Status == "invisible" ? "offline" : accepter.Status;

        var friendDto = new FriendDTO
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
        var request = await _context.Friendships
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
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == userId && f.AddresseeId == friendId) ||
                 (f.RequesterId == friendId && f.AddresseeId == userId)));

        if (friendship == null)
            return (false, null, null);

        Guid? deletedDmId = null;

        var dm = await _context.Teams
            .Include(t => t.Members)
            .Where(t => t.IsDirect && t.Members.Count == 2)
            .Where(t => t.Members.Any(m => m.UserId == userId) && t.Members.Any(m => m.UserId == friendId))
            .FirstOrDefaultAsync();

        if (dm != null)
        {
            deletedDmId = dm.Id;

            var messages = await _context.Messages
                .Where(m => m.Team != null && m.Team.Id == dm.Id)
                .ToListAsync();
            _context.Messages.RemoveRange(messages);

            var members = await _context.Members.Where(m => m.TeamId == dm.Id).ToListAsync();
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

    public async Task<IReadOnlyList<UserDTOPublic>> SearchFriendsAsync(string userId, string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        if (limit < 1) limit = 1;
        if (limit > 20) limit = 20;

        string normalizedQuery = query.Trim().ToLowerInvariant();

        var friendIds = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        var friends = await _context.Users
            .AsNoTracking()
            .Where(u => friendIds.Contains(u.Id) &&
                       (u.Name.ToLower().Contains(normalizedQuery) ||
                        (u.Email != null && u.Email.ToLower().Contains(normalizedQuery))))
            .OrderBy(u => u.Name)
            .Take(limit)
            .Select(u => new UserDTOPublic
            {
                Id = u.Id,
                Name = u.Name,
                Level = u.Level
            })
            .ToListAsync();

        return friends;
    }

    public async Task<IReadOnlyList<string>> GetPendingRequestUserIdsAsync(string userId)
    {
        var userIds = await _context.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending &&
                       (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();

        return userIds;
    }
}
