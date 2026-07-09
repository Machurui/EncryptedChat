using System.Text.Json;
using Microsoft.JSInterop;

namespace EncryptedChat.Client.Services;

public sealed class RecentGifsService(IJSRuntime js)
{
    private const string StorageKey = "encryptedchat.recentGifs";
    private const int MaxItems = 12;
    private readonly IJSRuntime _js = js;

    public sealed record RecentGif(string Url, string PreviewUrl, int Width = 0, int Height = 0);

    public async Task<List<RecentGif>> GetAllAsync()
    {
        try
        {
            string? raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) return [];
            return JsonSerializer.Deserialize<List<RecentGif>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task AddAsync(RecentGif gif)
    {
        try
        {
            List<RecentGif> list = await GetAllAsync();
            list.RemoveAll(g => g.Url == gif.Url);
            list.Insert(0, gif);

            if (list.Count > MaxItems) 
                list.RemoveRange(MaxItems, list.Count - MaxItems);

            string json = JsonSerializer.Serialize(list);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // LocalStorage unavailable (private mode etc.) — silent no-op
        }
    }
}
