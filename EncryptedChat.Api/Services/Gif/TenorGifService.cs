using System.Text.Json;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public sealed class TenorGifService : IGifService
{
    private const string TenorBaseUrl = "https://tenor.googleapis.com/v2/search";
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public TenorGifService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Gifs:TenorApiKey"] ?? string.Empty;
    }

    public async Task<List<GifResultDTO>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Tenor API key is not configured. Set Gifs:TenorApiKey in configuration.");

        var url = $"{TenorBaseUrl}?key={Uri.EscapeDataString(_apiKey)}" +
                  $"&q={Uri.EscapeDataString(query)}" +
                  $"&limit={limit}" +
                  $"&locale=fr_FR" +
                  $"&contentfilter=medium" +
                  $"&media_filter={Uri.EscapeDataString("gif,tinygif")}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var results = new List<GifResultDTO>();
        if (!doc.RootElement.TryGetProperty("results", out var resultsArray))
            return results;

        foreach (var item in resultsArray.EnumerateArray())
        {
            if (!item.TryGetProperty("media_formats", out var media)) continue;
            if (!media.TryGetProperty("gif", out var gif)) continue;
            if (!media.TryGetProperty("tinygif", out var tiny)) continue;

            var gifUrl = gif.TryGetProperty("url", out var gu) ? gu.GetString() : null;
            var previewUrl = tiny.TryGetProperty("url", out var tu) ? tu.GetString() : null;

            if (!string.IsNullOrEmpty(gifUrl) && !string.IsNullOrEmpty(previewUrl))
                results.Add(new GifResultDTO(gifUrl, previewUrl));
        }

        return results;
    }
}
