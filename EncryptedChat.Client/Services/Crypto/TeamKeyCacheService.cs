using System.Collections.Concurrent;

namespace EncryptedChat.Client.Services.Crypto;

public class TeamKeyCacheService
{
    private readonly ConcurrentDictionary<(Guid TeamId, int Generation), byte[]> _cache = new();

    public byte[]? Get(Guid teamId, int generation)
    {
        return _cache.TryGetValue((teamId, generation), out byte[]? secret) ? secret : null;
    }

    public bool Has(Guid teamId, int generation) => Get(teamId, generation) != null;

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
        foreach ((Guid, int) key in _cache.Keys.Where(k => k.TeamId == teamId).ToList())
        {
            _cache.TryRemove(key, out _);
        }
    }
}
