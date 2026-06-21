using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Services;

public sealed class BlindIndex : IBlindIndex
{
    private readonly byte[] _master;
    private readonly ConcurrentDictionary<string, byte[]> _subKeys = new();

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
        _master = master;
    }

    public string Compute(string normalizedValue, string purpose = "blind-index")
    {
        // Distinct HKDF sub-key per purpose (cached). "blind-index" reproduces the SP-C key.
        byte[] key = _subKeys.GetOrAdd(purpose, p => HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: _master,
            outputLength: 32,
            salt: null,
            info: Encoding.UTF8.GetBytes(p)));
        byte[] mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalizedValue ?? string.Empty));
        return Convert.ToBase64String(mac);
    }
}
