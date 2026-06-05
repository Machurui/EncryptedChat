using System.Text.Json;
using Microsoft.JSInterop;

namespace EncryptedChat.Client.Services.Crypto;

public class IndexedDbPinStore(IJSRuntime js) : IPinStore
{
    private readonly IJSRuntime _js = js;
    private static string KeyFor(string userId) => $"keypin:{userId}";

    public async Task<PinRecord?> GetAsync(string userId)
    {
        string? json = await _js.InvokeAsync<string?>("encryptedChatIdb.idbGet", KeyFor(userId));
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<PinRecord>(json);
    }

    public async Task SetAsync(string userId, PinRecord record)
    {
        string json = JsonSerializer.Serialize(record);
        await _js.InvokeVoidAsync("encryptedChatIdb.idbSet", KeyFor(userId), json);
    }
}
