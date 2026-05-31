using System.Collections.Concurrent;

namespace EncryptedChat.Client.Services.Crypto;

public class TeamKeyCacheService
{
    // (TeamId, Generation) → unwrapped Team.Secret (32 bytes AES-256 key)
    private readonly ConcurrentDictionary<(Guid TeamId, int Generation), byte[]> _cache = new();

    public byte[]? Get(Guid teamId, int generation)
    {
        return _cache.TryGetValue((teamId, generation), out var secret) ? secret : null;
    }

    public void Put(Guid teamId, int generation, byte[] teamSecret)
    {
        _cache[(teamId, generation)] = teamSecret;
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public void ClearTeam(Guid teamId)
    {
        foreach (var key in _cache.Keys.Where(k => k.TeamId == teamId).ToList())
        {
            _cache.TryRemove(key, out _);
        }
    }
}
