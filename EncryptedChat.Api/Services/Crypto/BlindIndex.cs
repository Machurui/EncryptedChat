using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Services;

public sealed class BlindIndex : IBlindIndex
{
    private readonly byte[] _key; // HMAC key, HKDF-derived from the master key

    public BlindIndex(IConfiguration config)
    {
        string? b64 = config["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException("Encryption:Key is not configured.");
        byte[] master;
        try { master = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new InvalidOperationException("Encryption:Key is not valid base64."); }
        if (master.Length != 32)
            throw new InvalidOperationException($"Encryption:Key must be 32 bytes; got {master.Length}.");

        // Distinct sub-key for blind indexing (cryptographic separation from the AES key).
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: master,
            outputLength: 32,
            salt: null,
            info: Encoding.UTF8.GetBytes("blind-index"));
    }

    public string Compute(string normalizedValue)
    {
        byte[] mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalizedValue ?? string.Empty));
        return Convert.ToBase64String(mac);
    }
}
