using System.Net.Http.Json;

namespace EncryptedChat.Client.Services;

public sealed class GifClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public sealed record GifResult(string Url, string PreviewUrl, int Width, int Height);
    public sealed record GifCategory(string Name, string PreviewGifUrl);

    private static string TypeParam(bool stickers) => stickers ? "stickers" : "gifs";

    public async Task<List<GifResult>> SearchAsync(string query, int limit = 20, int offset = 0, bool stickers = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        string url = $"api/gif/search?q={Uri.EscapeDataString(query.Trim())}&limit={limit}&offset={offset}&type={TypeParam(stickers)}";
        List<GifResult>? results = await _http.GetFromJsonAsync<List<GifResult>>(url, ct);
        return results ?? [];
    }

    public async Task<List<GifResult>> TrendingAsync(int limit = 20, int offset = 0, bool stickers = false, CancellationToken ct = default)
    {
        string url = $"api/gif/trending?limit={limit}&offset={offset}&type={TypeParam(stickers)}";
        List<GifResult>? results = await _http.GetFromJsonAsync<List<GifResult>>(url, ct);
        return results ?? [];
    }

    public async Task<GifResult?> RandomAsync(string? tag = null, bool stickers = false, CancellationToken ct = default)
    {
        string url = $"api/gif/random?type={TypeParam(stickers)}";
        if (!string.IsNullOrWhiteSpace(tag))
            url += $"&tag={Uri.EscapeDataString(tag.Trim())}";

        try
        {
            return await _http.GetFromJsonAsync<GifResult>(url, ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<GifCategory>> CategoriesAsync(CancellationToken ct = default)
    {
        List<GifCategory>? results = await _http.GetFromJsonAsync<List<GifCategory>>("api/gif/categories", ct);
        return results ?? [];
    }
}
