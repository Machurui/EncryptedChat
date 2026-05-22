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

    public async Task<List<GifResult>> SearchAsync(string query, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new();

        var url = $"api/gif/search?q={Uri.EscapeDataString(query.Trim())}&limit={limit}";
        var results = await _http.GetFromJsonAsync<List<GifResult>>(url, ct);
        return results ?? new();
    }
}
