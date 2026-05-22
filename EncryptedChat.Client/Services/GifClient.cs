using System.Net.Http.Json;

namespace EncryptedChat.Client.Services;

public sealed class GifClient
{
    private readonly HttpClient _http;

    public GifClient(HttpClient http)
    {
        _http = http;
    }

    public sealed record GifResult(string Url, string PreviewUrl);
    public sealed record GifCategory(string Name, string PreviewGifUrl);

    public async Task<List<GifResult>> SearchAsync(string query, int limit = 20, int offset = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new();

        var url = $"api/gif/search?q={Uri.EscapeDataString(query.Trim())}&limit={limit}&offset={offset}";
        var results = await _http.GetFromJsonAsync<List<GifResult>>(url, ct);
        return results ?? new();
    }

    public async Task<List<GifResult>> TrendingAsync(int limit = 20, int offset = 0, CancellationToken ct = default)
    {
        var url = $"api/gif/trending?limit={limit}&offset={offset}";
        var results = await _http.GetFromJsonAsync<List<GifResult>>(url, ct);
        return results ?? new();
    }

    public async Task<List<GifCategory>> CategoriesAsync(CancellationToken ct = default)
    {
        var results = await _http.GetFromJsonAsync<List<GifCategory>>("api/gif/categories", ct);
        return results ?? new();
    }
}
