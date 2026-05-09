using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class FriendService(EncryptedChatContext context) : IFriendService
{
    private readonly EncryptedChatContext _context = context;

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
            return new FriendDTO
            {
                UserId = friend!.Id,
                Name = friend.Name,
                Level = friend.Level,
                FriendsSince = f.AcceptedAt ?? f.CreatedAt
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
                Level = otherUser.Level,
                SentAt = f.CreatedAt,
                IsIncoming = isIncoming
            };
        }).ToList();
    }

    public async Task<bool> SendRequestAsync(string requesterId, string addresseeId)
    {
        if (requesterId == addresseeId)
            return false;

        bool addresseeExists = await _context.Users.AnyAsync(u => u.Id == addresseeId);
        if (!addresseeExists)
            return false;

        bool existingFriendship = await _context.Friendships
            .AnyAsync(f =>
                (f.RequesterId == requesterId && f.AddresseeId == addresseeId) ||
                (f.RequesterId == addresseeId && f.AddresseeId == requesterId));

        if (existingFriendship)
            return false;

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AcceptRequestAsync(string userId, Guid requestId)
    {
        var request = await _context.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId &&
                                      f.AddresseeId == userId &&
                                      f.Status == FriendshipStatus.Pending);

        if (request == null)
            return false;

        request.Status = FriendshipStatus.Accepted;
        request.AcceptedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectRequestAsync(string userId, Guid requestId)
    {
        var request = await _context.Friendships
            .FirstOrDefaultAsync(f => f.Id == requestId &&
                                      f.AddresseeId == userId &&
                                      f.Status == FriendshipStatus.Pending);

        if (request == null)
            return false;

        _context.Friendships.Remove(request);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFriendAsync(string userId, string friendId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == userId && f.AddresseeId == friendId) ||
                 (f.RequesterId == friendId && f.AddresseeId == userId)));

        if (friendship == null)
            return false;

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
        return true;
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
}
