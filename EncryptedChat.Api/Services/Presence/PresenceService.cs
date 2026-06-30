using System.Collections.Concurrent;

namespace EncryptedChat.Services;

public class PresenceService : IPresenceService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public bool IsOnline(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        if (!_connections.TryGetValue(userId, out HashSet<string>? set))
            return false;

        lock (set) { return set.Count > 0; }
    }

    public void AddConnection(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        _connections.AddOrUpdate(
            userId,
            _ => [connectionId],
            (_, set) => { lock (set) { set.Add(connectionId); } return set; });
    }

    public bool RemoveConnection(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        if (!_connections.TryGetValue(userId, out HashSet<string>? set))
            return false;

        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count == 0)
            {
                _connections.TryRemove(userId, out _);
                return true;
            }
        }
        return false;
    }
}
