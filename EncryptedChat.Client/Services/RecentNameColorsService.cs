using System.Text.Json;
using Microsoft.JSInterop;

namespace EncryptedChat.Client.Services;

public sealed class RecentNameColorsService(IJSRuntime js)
{
    private const string StorageKey = "encryptedchat.recentNameColors";
    private const int MaxItems = 8;
    private readonly IJSRuntime _js = js;

    public async Task<List<string>> GetAllAsync()
    {
        try
        {
            string? raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) 
                return [];

            return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task AddAsync(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) 
            return;
        try
        {
            List<string> list = await GetAllAsync();
            list.RemoveAll(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, color);

            if (list.Count > MaxItems) 
                list.RemoveRange(MaxItems, list.Count - MaxItems);

            string json = JsonSerializer.Serialize(list);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // LocalStorage unavailable — silent no-op
        }
    }
}
