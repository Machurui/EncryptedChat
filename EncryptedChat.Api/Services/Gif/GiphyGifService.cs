using System.Text.Json;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public sealed class GiphyGifService(HttpClient http, IConfiguration config) : IGifService
{
    private const string GiphyCategoriesUrl = "https://api.giphy.com/v1/gifs/categories";
    private readonly HttpClient _http = http;
    private readonly string _apiKey = config["Giphy:ServiceApiKey"] ?? string.Empty;

    private static string Base(bool stickers, string kind) =>
        $"https://api.giphy.com/v1/{(stickers ? "stickers" : "gifs")}/{kind}";

    public async Task<List<GifResultDTO>> SearchAsync(string query, int limit, int offset, bool stickers, CancellationToken ct)
    {
        EnsureApiKey();
        string url = $"{Base(stickers, "search")}?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&q={Uri.EscapeDataString(query)}" +
                  $"&limit={limit}" +
                  $"&offset={offset}" +
                  $"&rating=pg-13" +
                  $"&lang=fr";
        return await FetchAndParseGifsAsync(url, ct);
    }

    public async Task<List<GifResultDTO>> TrendingAsync(int limit, int offset, bool stickers, CancellationToken ct)
    {
        EnsureApiKey();
        string url = $"{Base(stickers, "trending")}?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&limit={limit}" +
                  $"&offset={offset}" +
                  $"&rating=pg-13";
        return await FetchAndParseGifsAsync(url, ct);
    }

    public async Task<GifResultDTO?> RandomAsync(string? tag, bool stickers, CancellationToken ct)
    {
        EnsureApiKey();
        string url = $"{Base(stickers, "random")}?api_key={Uri.EscapeDataString(_apiKey)}&rating=pg-13";
        if (!string.IsNullOrWhiteSpace(tag))
            url += $"&tag={Uri.EscapeDataString(tag.Trim())}";

        using HttpResponseMessage response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object)
            return null;

        return ParseGifItem(data);
    }

    public async Task<List<GifCategoryDTO>> CategoriesAsync(CancellationToken ct)
    {
        EnsureApiKey();
        string url = $"{GiphyCategoriesUrl}?api_key={Uri.EscapeDataString(_apiKey)}";

        using HttpResponseMessage response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        List<GifCategoryDTO> results = [];
        if (!doc.RootElement.TryGetProperty("data", out JsonElement dataArray))
            return results;

        foreach (JsonElement item in dataArray.EnumerateArray())
        {
            string? name = item.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
            string? previewUrl = null;
            if (item.TryGetProperty("gif", out JsonElement gif) &&
                gif.TryGetProperty("images", out JsonElement images) &&
                images.TryGetProperty("fixed_width_small", out JsonElement fws) &&
                fws.TryGetProperty("url", out JsonElement urlProp))
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
        using HttpResponseMessage response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        List<GifResultDTO> results = [];
        if (!doc.RootElement.TryGetProperty("data", out JsonElement dataArray))
            return results;

        foreach (JsonElement item in dataArray.EnumerateArray())
        {
            GifResultDTO? dto = ParseGifItem(item);
            if (dto is not null)
                results.Add(dto);
        }

        return results;
    }

    private static GifResultDTO? ParseGifItem(JsonElement item)
    {
        if (!item.TryGetProperty("images", out JsonElement images)) return null;
        if (!images.TryGetProperty("original", out JsonElement original)) return null;
        if (!images.TryGetProperty("fixed_width", out JsonElement preview)) return null;

        string? gifUrl = original.TryGetProperty("url", out JsonElement ou) ? ou.GetString() : null;
        string? previewUrl = preview.TryGetProperty("url", out JsonElement pu) ? pu.GetString() : null;
        int width = ParseDimension(preview, "width");
        int height = ParseDimension(preview, "height");

        if (string.IsNullOrEmpty(gifUrl) || string.IsNullOrEmpty(previewUrl))
            return null;

        return new GifResultDTO(gifUrl, previewUrl, width, height);
    }

    private static int ParseDimension(JsonElement parent, string propertyName)
    {
        // Giphy returns dimensions as strings (e.g. "200") inside the rendition object.
        if (!parent.TryGetProperty(propertyName, out JsonElement prop))
            return 0;

        string? raw = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();

        return int.TryParse(raw, out int n) && n > 0 ? n : 0;
    }
}
