using System.Text.Json;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public sealed class GiphyGifService : IGifService
{
    private const string GiphyBaseUrl = "https://api.giphy.com/v1/gifs/search";
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GiphyGifService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Giphy:ServiceApiKey"] ?? string.Empty;
    }

    public async Task<List<GifResultDTO>> SearchAsync(string query, int limit, int offset, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Giphy API key is not configured. Set Giphy:ServiceApiKey in configuration.");

        var url = $"{GiphyBaseUrl}?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&q={Uri.EscapeDataString(query)}" +
                  $"&limit={limit}" +
                  $"&offset={offset}" +
                  $"&rating=pg-13" +
                  $"&lang=fr";

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
            if (!images.TryGetProperty("fixed_width_small", out var preview)) continue;

            var gifUrl = original.TryGetProperty("url", out var ou) ? ou.GetString() : null;
            var previewUrl = preview.TryGetProperty("url", out var pu) ? pu.GetString() : null;

            if (!string.IsNullOrEmpty(gifUrl) && !string.IsNullOrEmpty(previewUrl))
                results.Add(new GifResultDTO(gifUrl, previewUrl));
        }

        return results;
    }
}
