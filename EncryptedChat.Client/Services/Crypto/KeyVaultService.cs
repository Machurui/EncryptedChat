using System.Text.Json;
using Microsoft.JSInterop;

namespace EncryptedChat.Client.Services.Crypto;

public class KeyVaultService(IJSRuntime js)
{
    private readonly IJSRuntime _js = js;

    public record StoredKeys(byte[] SigningPrivateKey, byte[] EncryptionPrivateKey);

    private static string KeyFor(string userId) => $"user:{userId}";

    public async Task<StoredKeys?> GetMyKeysAsync(string userId)
    {
        string? json = await _js.InvokeAsync<string?>("encryptedChatIdb.idbGet", KeyFor(userId));
        if (string.IsNullOrEmpty(json)) return null;

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        return new StoredKeys(
            Convert.FromBase64String(root.GetProperty("sign").GetString()!),
            Convert.FromBase64String(root.GetProperty("enc").GetString()!));
    }

    public async Task StoreMyKeysAsync(string userId, byte[] signingPriv, byte[] encryptionPriv)
    {
        BundleEncryption payload = new
        (
            Signature: Convert.ToBase64String(signingPriv),
            Encryption: Convert.ToBase64String(encryptionPriv)
        );
        string json = JsonSerializer.Serialize(payload);
        await _js.InvokeVoidAsync("encryptedChatIdb.idbSet", KeyFor(userId), json);
    }

    public async Task ClearMyKeysAsync(string userId)
    {
        await _js.InvokeVoidAsync("encryptedChatIdb.idbDelete", KeyFor(userId));
    }

    public async Task<bool> IsBootstrappedAsync(string userId)
    {
        return await GetMyKeysAsync(userId) != null;
    }

    public record BundleEncryption (
        string Signature,
        string Encryption
    );
}
