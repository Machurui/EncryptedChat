namespace EncryptedChat.Client.Services;

public sealed record GifItem(string Url, string PreviewUrl, int Width, int Height, string Type, long Ts);

public sealed record GifTomb(string Url, long RemovedTs);

public sealed class GifVaultState
{
    public List<GifItem> Favorites { get; set; } = [];
    public List<GifItem> Recents { get; set; } = [];
    public List<GifTomb> Tombstones { get; set; } = [];
}

public static class GifVaultMerge
{
    public const int MaxRecents = 30;
    public const int MaxFavorites = 500;
    public const int MaxTombstones = 100;
    public const long TombstoneTtlMs = 90L * 24 * 60 * 60 * 1000;

    public static GifVaultState Merge(GifVaultState local, GifVaultState remote, long nowMs)
    {
        // 1) Latest removal timestamp per Url across both sides.
        Dictionary<string, long> tombs = [];
        foreach (GifTomb t in local.Tombstones.Concat(remote.Tombstones))
            if (!tombs.TryGetValue(t.Url, out long ts) || t.RemovedTs > ts)
                tombs[t.Url] = t.RemovedTs;

        // 2) Latest favorite add per Url across both sides.
        Dictionary<string, GifItem> favAdds = [];
        foreach (GifItem f in local.Favorites.Concat(remote.Favorites))
            if (!favAdds.TryGetValue(f.Url, out GifItem? cur) || f.Ts > cur.Ts)
                favAdds[f.Url] = f;

        // 3) A favorite survives only if its add is at least as new as any tombstone.
        List<GifItem> favorites = [];
        foreach ((string url, GifItem item) in favAdds)
        {
            long removedTs = tombs.TryGetValue(url, out long rt) ? rt : long.MinValue;
            if (item.Ts >= removedTs)
            {
                favorites.Add(item);
                tombs.Remove(url);
            }
        }
        favorites = favorites.OrderByDescending(f => f.Ts).Take(MaxFavorites).ToList();

        // 4) Recents: newest Ts per Url, keep MaxRecents most recent.
        Dictionary<string, GifItem> recentMap = [];
        foreach (GifItem r in local.Recents.Concat(remote.Recents))
            if (!recentMap.TryGetValue(r.Url, out GifItem? cur) || r.Ts > cur.Ts)
                recentMap[r.Url] = r;

        List<GifItem> recents = [.. recentMap.Values
            .OrderByDescending(r => r.Ts)
            .Take(MaxRecents)];

        // 5) Prune tombstones: drop those older than TTL, then cap to the newest MaxTombstones.
        List<GifTomb> tombstones = [.. tombs
            .Where(kv => nowMs - kv.Value <= TombstoneTtlMs)
            .Select(kv => new GifTomb(kv.Key, kv.Value))
            .OrderByDescending(t => t.RemovedTs)
            .Take(MaxTombstones)];

        return new GifVaultState { Favorites = favorites, Recents = recents, Tombstones = tombstones };
    }
}
