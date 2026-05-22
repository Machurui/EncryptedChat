using EncryptedChat.Models;
using Microsoft.Extensions.Caching.Memory;

namespace EncryptedChat.Services;

public sealed class GifCacheDecorator(IGifService inner, IMemoryCache cache) : IGifService
{
    private static readonly TimeSpan TrendingTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan CategoriesTtl = TimeSpan.FromHours(24);

    private readonly IGifService _inner = inner;
    private readonly IMemoryCache _cache = cache;

    public Task<List<GifResultDTO>> SearchAsync(string query, int limit, int offset, CancellationToken ct)
        => _inner.SearchAsync(query, limit, offset, ct);

    public async Task<List<GifResultDTO>> TrendingAsync(int limit, int offset, CancellationToken ct)
    {
        var key = $"gif:trending:{limit}:{offset}";
        if (_cache.TryGetValue(key, out List<GifResultDTO>? cached) && cached is not null)
            return cached;

        var result = await _inner.TrendingAsync(limit, offset, ct);
        _cache.Set(key, result, TrendingTtl);
        return result;
    }

    public async Task<List<GifCategoryDTO>> CategoriesAsync(CancellationToken ct)
    {
        const string key = "gif:categories";
        if (_cache.TryGetValue(key, out List<GifCategoryDTO>? cached) && cached is not null)
            return cached;

        var result = await _inner.CategoriesAsync(ct);
        _cache.Set(key, result, CategoriesTtl);
        return result;
    }
}
