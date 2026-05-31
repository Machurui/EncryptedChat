using System.Text;
using Microsoft.JSInterop;

namespace EncryptedChat.Client.Services.Crypto;

// Bridge to wwwroot/js/crypto-interop.js (WebCrypto API). All primitives are
// async because SubtleCrypto returns Promises and Blazor WASM has no native
// crypto backend for ECDsa/ECDH/AES-GCM. Binary data crosses JSInterop as
// base64 strings to avoid Uint8Array marshalling quirks.
public class CryptoService(IJSRuntime js)
{
    private readonly IJSRuntime _js = js;

    // ---------- Identity keypair generation ----------

    public record IdentityKeyPair(
        byte[] SigningPrivateKey,    // PKCS#8
        byte[] SigningPublicKey,     // SPKI
        byte[] EncryptionPrivateKey, // PKCS#8
        byte[] EncryptionPublicKey); // SPKI

    private record IdentityKeyPairResponse(
        string SigningPrivateKey, string SigningPublicKey,
        string EncryptionPrivateKey, string EncryptionPublicKey);

    public async Task<IdentityKeyPair> GenerateIdentityKeyPairAsync()
    {
        var resp = await _js.InvokeAsync<IdentityKeyPairResponse>(
            "encryptedChatCrypto.generateIdentityKeyPair");
        return new IdentityKeyPair(
            SigningPrivateKey: Convert.FromBase64String(resp.SigningPrivateKey),
            SigningPublicKey: Convert.FromBase64String(resp.SigningPublicKey),
            EncryptionPrivateKey: Convert.FromBase64String(resp.EncryptionPrivateKey),
            EncryptionPublicKey: Convert.FromBase64String(resp.EncryptionPublicKey));
    }

    // ---------- Team secret / salt / randomness ----------

    public async Task<byte[]> GenerateTeamSecretAsync()
    {
        string b64 = await _js.InvokeAsync<string>("encryptedChatCrypto.generateTeamSecret");
        return Convert.FromBase64String(b64);
    }

    public async Task<byte[]> GenerateSaltAsync()
    {
        string b64 = await _js.InvokeAsync<string>("encryptedChatCrypto.generateSalt");
        return Convert.FromBase64String(b64);
    }

    // ---------- AES-256-GCM ----------

    public record AesGcmCiphertext(byte[] Iv, byte[] CiphertextWithTag);

    private record AesGcmJsResult(string Iv, string CiphertextWithTag);

    public async Task<AesGcmCiphertext> EncryptAesGcmAsync(byte[] plaintext, byte[] key)
    {
        var r = await _js.InvokeAsync<AesGcmJsResult>(
            "encryptedChatCrypto.encryptAesGcm",
            Convert.ToBase64String(plaintext),
            Convert.ToBase64String(key));
        return new AesGcmCiphertext(
            Convert.FromBase64String(r.Iv),
            Convert.FromBase64String(r.CiphertextWithTag));
    }

    public async Task<byte[]> DecryptAesGcmAsync(byte[] iv, byte[] ciphertextWithTag, byte[] key)
    {
        string b64 = await _js.InvokeAsync<string>(
            "encryptedChatCrypto.decryptAesGcm",
            Convert.ToBase64String(iv),
            Convert.ToBase64String(ciphertextWithTag),
            Convert.ToBase64String(key));
        return Convert.FromBase64String(b64);
    }

    // ---------- ECDSA sign / verify ----------

    public async Task<byte[]> SignAsync(byte[] data, byte[] signingPrivateKeyPkcs8)
    {
        string b64 = await _js.InvokeAsync<string>(
            "encryptedChatCrypto.sign",
            Convert.ToBase64String(data),
            Convert.ToBase64String(signingPrivateKeyPkcs8));
        return Convert.FromBase64String(b64);
    }

    public async Task<bool> VerifyAsync(byte[] data, byte[] signature, byte[] signingPublicKeySpki)
    {
        return await _js.InvokeAsync<bool>(
            "encryptedChatCrypto.verify",
            Convert.ToBase64String(data),
            Convert.ToBase64String(signature),
            Convert.ToBase64String(signingPublicKeySpki));
    }

    public async Task<byte[]> Sha256Async(byte[] data)
    {
        string b64 = await _js.InvokeAsync<string>(
            "encryptedChatCrypto.sha256", Convert.ToBase64String(data));
        return Convert.FromBase64String(b64);
    }

    // ---------- ECIES-P256 wrap / unwrap ----------

    public async Task<byte[]> WrapKeyAsync(byte[] keyToWrap, byte[] recipientEncryptionPublicKeySpki)
    {
        string b64 = await _js.InvokeAsync<string>(
            "encryptedChatCrypto.wrapKey",
            Convert.ToBase64String(keyToWrap),
            Convert.ToBase64String(recipientEncryptionPublicKeySpki));
        return Convert.FromBase64String(b64);
    }

    public async Task<byte[]> UnwrapKeyAsync(byte[] wrapped, byte[] recipientEncryptionPrivateKeyPkcs8)
    {
        string b64 = await _js.InvokeAsync<string>(
            "encryptedChatCrypto.unwrapKey",
            Convert.ToBase64String(wrapped),
            Convert.ToBase64String(recipientEncryptionPrivateKeyPkcs8));
        return Convert.FromBase64String(b64);
    }

    // ---------- Phrase-derived wrap-key ----------

    public async Task<byte[]> DeriveWrapKeyAsync(string phrase, byte[] salt)
    {
        string b64 = await _js.InvokeAsync<string>(
            "encryptedChatCrypto.deriveWrapKey",
            phrase,
            Convert.ToBase64String(salt));
        return Convert.FromBase64String(b64);
    }

    // ---------- Identity-bundle wrap / unwrap ----------

    public async Task<byte[]> WrapIdentityPrivateKeysAsync(byte[] signingPrivPkcs8, byte[] encPrivPkcs8, byte[] wrapKey)
    {
        var bundle = new
        {
            sign = Convert.ToBase64String(signingPrivPkcs8),
            enc = Convert.ToBase64String(encPrivPkcs8)
        };
        byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(bundle);
        AesGcmCiphertext wrapped = await EncryptAesGcmAsync(json, wrapKey);

        byte[] result = new byte[wrapped.Iv.Length + wrapped.CiphertextWithTag.Length];
        Buffer.BlockCopy(wrapped.Iv, 0, result, 0, wrapped.Iv.Length);
        Buffer.BlockCopy(wrapped.CiphertextWithTag, 0, result, wrapped.Iv.Length, wrapped.CiphertextWithTag.Length);
        return result;
    }

    public async Task<(byte[] SigningPrivateKey, byte[] EncryptionPrivateKey)> UnwrapIdentityPrivateKeysAsync(byte[] wrappedBundle, byte[] wrapKey)
    {
        const int ivLen = 12;
        byte[] iv = new byte[ivLen];
        Buffer.BlockCopy(wrappedBundle, 0, iv, 0, ivLen);
        byte[] ciphertext = new byte[wrappedBundle.Length - ivLen];
        Buffer.BlockCopy(wrappedBundle, ivLen, ciphertext, 0, ciphertext.Length);

        byte[] json = await DecryptAesGcmAsync(iv, ciphertext, wrapKey);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            Convert.FromBase64String(root.GetProperty("sign").GetString()!),
            Convert.FromBase64String(root.GetProperty("enc").GetString()!));
    }

    // ---------- Message envelope helpers ----------

    public record MessageEnvelope(string EncryptedText, string Iv, string Signature, int KeyGeneration);

    public async Task<MessageEnvelope> EncryptAndSignMessageAsync(
        string plaintext,
        byte[] teamSecret,
        int keyGeneration,
        byte[] signingPrivateKey,
        Guid teamId,
        string senderId)
    {
        AesGcmCiphertext encrypted = await EncryptAesGcmAsync(Encoding.UTF8.GetBytes(plaintext), teamSecret);

        byte[] sigInput = await BuildSignatureInputAsync(
            encrypted.CiphertextWithTag, encrypted.Iv, teamId, senderId, keyGeneration);
        byte[] sig = await SignAsync(sigInput, signingPrivateKey);

        return new MessageEnvelope(
            EncryptedText: Convert.ToBase64String(encrypted.CiphertextWithTag),
            Iv: Convert.ToBase64String(encrypted.Iv),
            Signature: Convert.ToBase64String(sig),
            KeyGeneration: keyGeneration);
    }

    public async Task<string> DecryptAndVerifyMessageAsync(
        MessageEnvelope envelope,
        byte[] teamSecret,
        byte[] senderSigningPublicKey,
        Guid teamId,
        string senderId)
    {
        byte[] ciphertext = Convert.FromBase64String(envelope.EncryptedText);
        byte[] iv = Convert.FromBase64String(envelope.Iv);
        byte[] sig = Convert.FromBase64String(envelope.Signature);

        byte[] sigInput = await BuildSignatureInputAsync(ciphertext, iv, teamId, senderId, envelope.KeyGeneration);
        if (!await VerifyAsync(sigInput, sig, senderSigningPublicKey))
            throw new InvalidOperationException("Message signature verification failed");

        return Encoding.UTF8.GetString(await DecryptAesGcmAsync(iv, ciphertext, teamSecret));
    }

    private async Task<byte[]> BuildSignatureInputAsync(byte[] ciphertext, byte[] iv, Guid teamId, string senderId, int keyGen)
    {
        byte[] teamBytes = teamId.ToByteArray();
        byte[] senderBytes = Encoding.UTF8.GetBytes(senderId);
        byte[] genBytes = BitConverter.GetBytes(keyGen);

        byte[] toHash = new byte[ciphertext.Length + iv.Length + teamBytes.Length + senderBytes.Length + genBytes.Length];
        int offset = 0;
        Buffer.BlockCopy(ciphertext, 0, toHash, offset, ciphertext.Length); offset += ciphertext.Length;
        Buffer.BlockCopy(iv, 0, toHash, offset, iv.Length); offset += iv.Length;
        Buffer.BlockCopy(teamBytes, 0, toHash, offset, teamBytes.Length); offset += teamBytes.Length;
        Buffer.BlockCopy(senderBytes, 0, toHash, offset, senderBytes.Length); offset += senderBytes.Length;
        Buffer.BlockCopy(genBytes, 0, toHash, offset, genBytes.Length);

        return await Sha256Async(toHash);
    }
}
