namespace EncryptedChat.Client.Services;

public sealed record GifItem(string Url, string PreviewUrl, int Width, int Height, string Type, long Ts);

public sealed record GifTomb(string Url, long RemovedTs);

public sealed class GifVaultState
{
    public List<GifItem> Favorites { get; set; } = new();
    public List<GifItem> Recents { get; set; } = new();
    public List<GifTomb> Tombstones { get; set; } = new();
}

public static class GifVaultMerge
{
    public const int MaxRecents = 30;
    public const int MaxTombstones = 100;
    public const long TombstoneTtlMs = 90L * 24 * 60 * 60 * 1000;

    public static GifVaultState Merge(GifVaultState local, GifVaultState remote, long nowMs)
    {
        // 1) Latest removal timestamp per Url across both sides.
        var tombs = new Dictionary<string, long>();
        foreach (var t in local.Tombstones.Concat(remote.Tombstones))
            if (!tombs.TryGetValue(t.Url, out var ts) || t.RemovedTs > ts)
                tombs[t.Url] = t.RemovedTs;

        // 2) Latest favorite add per Url across both sides.
        var favAdds = new Dictionary<string, GifItem>();
        foreach (var f in local.Favorites.Concat(remote.Favorites))
            if (!favAdds.TryGetValue(f.Url, out var cur) || f.Ts > cur.Ts)
                favAdds[f.Url] = f;

        // 3) A favorite survives only if its add is at least as new as any tombstone.
        var favorites = new List<GifItem>();
        foreach (var (url, item) in favAdds)
        {
            var removedTs = tombs.TryGetValue(url, out var rt) ? rt : long.MinValue;
            if (item.Ts >= removedTs)
            {
                favorites.Add(item);
                tombs.Remove(url); // add won → tombstone obsolete
            }
        }
        favorites = favorites.OrderByDescending(f => f.Ts).ToList();

        // 4) Recents: newest Ts per Url, keep MaxRecents most recent.
        var recentMap = new Dictionary<string, GifItem>();
        foreach (var r in local.Recents.Concat(remote.Recents))
            if (!recentMap.TryGetValue(r.Url, out var cur) || r.Ts > cur.Ts)
                recentMap[r.Url] = r;
        var recents = recentMap.Values
            .OrderByDescending(r => r.Ts)
            .Take(MaxRecents)
            .ToList();

        // 5) Prune tombstones: drop those older than TTL, then cap to the newest MaxTombstones.
        var tombstones = tombs
            .Where(kv => nowMs - kv.Value <= TombstoneTtlMs)
            .Select(kv => new GifTomb(kv.Key, kv.Value))
            .OrderByDescending(t => t.RemovedTs)
            .Take(MaxTombstones)
            .ToList();

        return new GifVaultState { Favorites = favorites, Recents = recents, Tombstones = tombstones };
    }
}
