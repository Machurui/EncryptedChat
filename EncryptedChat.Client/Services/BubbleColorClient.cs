using System.Net.Http.Json;

namespace EncryptedChat.Client.Services;

public class BubbleColorClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<Dictionary<Guid, string>> GetMyBubbleColorsAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/user/me/bubble-colors");
            if (!res.IsSuccessStatusCode)
                return new Dictionary<Guid, string>();

            var map = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
            return map ?? new Dictionary<Guid, string>();
        }
        catch (HttpRequestException)
        {
            return new Dictionary<Guid, string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return new Dictionary<Guid, string>();
        }
    }

    public async Task<bool> SetBubbleColorAsync(Guid teamId, string? color)
    {
        try
        {
            var res = await _http.PutAsJsonAsync(
                $"api/user/me/teams/{teamId}/bubble-color",
                new { Color = color });
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
