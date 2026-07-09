using System.Net.Http.Json;

namespace EncryptedChat.Client.Services;

public class BubbleColorClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<Dictionary<Guid, string>> GetMyBubbleColorsAsync()
    {
        try
        {
            HttpResponseMessage res = await _http.GetAsync("api/user/me/bubble-colors");
            if (!res.IsSuccessStatusCode)
                return [];

            Dictionary<Guid, string>? map = await res.Content.ReadFromJsonAsync<Dictionary<Guid, string>>();
            return map ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public async Task<bool> SetBubbleColorAsync(Guid teamId, string? color)
    {
        try
        {
            HttpResponseMessage res = await _http.PutAsJsonAsync(
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
