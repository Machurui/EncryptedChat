using System.Text.Json;
using System.Text.Json.Serialization;
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

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string? signingKey = ReadString(root, "sign", "Signature");
            string? encryptionKey = ReadString(root, "enc", "Encryption");

            if (string.IsNullOrWhiteSpace(signingKey) || string.IsNullOrWhiteSpace(encryptionKey))
                return null;

            return new StoredKeys(
                Convert.FromBase64String(signingKey),
                Convert.FromBase64String(encryptionKey));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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

    public record BundleEncryption(
        [property: JsonPropertyName("sign")] string Signature,
        [property: JsonPropertyName("enc")] string Encryption
    );

    private static string? ReadString(JsonElement root, string currentName, string legacyName)
    {
        if (root.TryGetProperty(currentName, out JsonElement current))
            return current.GetString();

        return root.TryGetProperty(legacyName, out JsonElement legacy)
            ? legacy.GetString()
            : null;
    }
}
