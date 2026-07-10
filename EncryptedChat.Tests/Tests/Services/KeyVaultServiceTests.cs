using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;
using FluentAssertions;
using Microsoft.JSInterop;

namespace EncryptedChat.Tests.Tests.Services;

public class KeyVaultServiceTests
{
    [Fact]
    public async Task StoreThenGet_RoundTripsUsingStablePropertyNames()
    {
        InMemoryJsRuntime js = new();
        KeyVaultService vault = new(js);
        byte[] signingKey = [1, 2, 3, 4];
        byte[] encryptionKey = [5, 6, 7, 8];

        await vault.StoreMyKeysAsync("user-1", signingKey, encryptionKey);
        KeyVaultService.StoredKeys? result = await vault.GetMyKeysAsync("user-1");

        result.Should().NotBeNull();
        result!.SigningPrivateKey.Should().Equal(signingKey);
        result.EncryptionPrivateKey.Should().Equal(encryptionKey);

        using JsonDocument stored = JsonDocument.Parse(js.StoredJson!);
        stored.RootElement.TryGetProperty("sign", out _).Should().BeTrue();
        stored.RootElement.TryGetProperty("enc", out _).Should().BeTrue();
        stored.RootElement.TryGetProperty("Signature", out _).Should().BeFalse();
        stored.RootElement.TryGetProperty("Encryption", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetMyKeys_ReadsLegacyPropertyNamesAlreadyStoredOnDevices()
    {
        byte[] signingKey = [9, 10, 11];
        byte[] encryptionKey = [12, 13, 14];
        InMemoryJsRuntime js = new()
        {
            StoredJson = JsonSerializer.Serialize(new
            {
                Signature = Convert.ToBase64String(signingKey),
                Encryption = Convert.ToBase64String(encryptionKey)
            })
        };
        KeyVaultService vault = new(js);

        KeyVaultService.StoredKeys? result = await vault.GetMyKeysAsync("user-1");

        result.Should().NotBeNull();
        result!.SigningPrivateKey.Should().Equal(signingKey);
        result.EncryptionPrivateKey.Should().Equal(encryptionKey);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"sign\":\"not-base64\",\"enc\":\"not-base64\"}")]
    public async Task GetMyKeys_ReturnsNullForCorruptStorage(string storedJson)
    {
        InMemoryJsRuntime js = new() { StoredJson = storedJson };
        KeyVaultService vault = new(js);

        KeyVaultService.StoredKeys? result = await vault.GetMyKeysAsync("user-1");

        result.Should().BeNull();
    }

    private sealed class InMemoryJsRuntime : IJSRuntime
    {
        public string? StoredJson { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "encryptedChatIdb.idbGet")
                return ValueTask.FromResult((TValue)(object?)StoredJson!);

            if (identifier == "encryptedChatIdb.idbSet")
            {
                StoredJson = args?[1] as string;
                return ValueTask.FromResult(default(TValue)!);
            }

            if (identifier == "encryptedChatIdb.idbDelete")
            {
                StoredJson = null;
                return ValueTask.FromResult(default(TValue)!);
            }

            throw new InvalidOperationException($"Unexpected JS invocation: {identifier}");
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
