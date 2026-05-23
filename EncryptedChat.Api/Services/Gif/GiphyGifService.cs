using System.Text.Json;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public sealed class GiphyGifService : IGifService
{
    private const string GiphySearchUrl = "https://api.giphy.com/v1/gifs/search";
    private const string GiphyTrendingUrl = "https://api.giphy.com/v1/gifs/trending";
    private const string GiphyCategoriesUrl = "https://api.giphy.com/v1/gifs/categories";
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GiphyGifService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Giphy:ServiceApiKey"] ?? string.Empty;
    }

    public async Task<List<GifResultDTO>> SearchAsync(string query, int limit, int offset, CancellationToken ct)
    {
        EnsureApiKey();
        var url = $"{GiphySearchUrl}?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&q={Uri.EscapeDataString(query)}" +
                  $"&limit={limit}" +
                  $"&offset={offset}" +
                  $"&rating=pg-13" +
                  $"&lang=fr";
        return await FetchAndParseGifsAsync(url, ct);
    }

    public async Task<List<GifResultDTO>> TrendingAsync(int limit, int offset, CancellationToken ct)
    {
        EnsureApiKey();
        var url = $"{GiphyTrendingUrl}?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&limit={limit}" +
                  $"&offset={offset}" +
                  $"&rating=pg-13";
        return await FetchAndParseGifsAsync(url, ct);
    }

    public async Task<List<GifCategoryDTO>> CategoriesAsync(CancellationToken ct)
    {
        EnsureApiKey();
        var url = $"{GiphyCategoriesUrl}?api_key={Uri.EscapeDataString(_apiKey)}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var results = new List<GifCategoryDTO>();
        if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            return results;

        foreach (var item in dataArray.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
            string? previewUrl = null;
            if (item.TryGetProperty("gif", out var gif) &&
                gif.TryGetProperty("images", out var images) &&
                images.TryGetProperty("fixed_width_small", out var fws) &&
                fws.TryGetProperty("url", out var urlProp))
            {
                previewUrl = urlProp.GetString();
            }

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(previewUrl))
                results.Add(new GifCategoryDTO(name, previewUrl));
        }

        return results;
    }

    private void EnsureApiKey()
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Giphy API key is not configured. Set Giphy:ServiceApiKey in configuration.");
    }

    private async Task<List<GifResultDTO>> FetchAndParseGifsAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var results = new List<GifResultDTO>();
        if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            return results;

        foreach (var item in dataArray.EnumerateArray())
        {
            if (!item.TryGetProperty("images", out var images)) continue;
            if (!images.TryGetProperty("original", out var original)) continue;
            if (!images.TryGetProperty("fixed_width", out var preview)) continue;

            var gifUrl = original.TryGetProperty("url", out var ou) ? ou.GetString() : null;
            var previewUrl = preview.TryGetProperty("url", out var pu) ? pu.GetString() : null;
            int width = ParseDimension(preview, "width");
            int height = ParseDimension(preview, "height");

            if (!string.IsNullOrEmpty(gifUrl) && !string.IsNullOrEmpty(previewUrl))
                results.Add(new GifResultDTO(gifUrl, previewUrl, width, height));
        }

        return results;
    }

    private static int ParseDimension(JsonElement parent, string propertyName)
    {
        // Giphy returns dimensions as strings (e.g. "200") inside the rendition object.
        if (!parent.TryGetProperty(propertyName, out var prop)) return 0;
        var raw = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        return int.TryParse(raw, out var n) && n > 0 ? n : 0;
    }
}
