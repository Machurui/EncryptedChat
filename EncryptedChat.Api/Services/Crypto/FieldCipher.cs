using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Services;

public sealed class FieldCipher : IFieldCipher
{
    private const byte Version = 0x01;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 1 + NonceSize + TagSize;

    private readonly byte[] _key;

    public FieldCipher(IConfiguration config)
    {
        string? b64 = config["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException("Encryption:Key is not configured.");
        byte[] key;
        try { key = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new InvalidOperationException("Encryption:Key is not valid base64."); }
        if (key.Length != 32)
            throw new InvalidOperationException($"Encryption:Key must be 32 bytes (AES-256); got {key.Length}.");
        _key = key;
    }

    public string? Encrypt(string? plaintext, string aad)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        byte[] pt = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ct = new byte[pt.Length];
        byte[] tag = new byte[TagSize];
        byte[] ad = Encoding.UTF8.GetBytes(aad ?? string.Empty);

        using (AesGcm aes = new(_key, TagSize))
            aes.Encrypt(nonce, pt, ct, tag, ad);

        byte[] blob = new byte[HeaderSize + ct.Length];
        blob[0] = Version;
        Buffer.BlockCopy(nonce, 0, blob, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(ct, 0, blob, HeaderSize, ct.Length);
        return Convert.ToBase64String(blob);
    }

    public string? Decrypt(string? stored, string aad)
    {
        if (string.IsNullOrEmpty(stored)) return stored;

        byte[] blob;
        try { blob = Convert.FromBase64String(stored); }
        catch (FormatException) { return stored; }

        if (blob.Length < HeaderSize || blob[0] != Version)
            return stored;

        byte[] nonce = new byte[NonceSize];
        byte[] tag = new byte[TagSize];
        int ctLen = blob.Length - HeaderSize;
        byte[] ct = new byte[ctLen];
        Buffer.BlockCopy(blob, 1, nonce, 0, NonceSize);
        Buffer.BlockCopy(blob, 1 + NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(blob, HeaderSize, ct, 0, ctLen);

        byte[] ad = Encoding.UTF8.GetBytes(aad ?? string.Empty);
        byte[] pt = new byte[ctLen];
        using (AesGcm aes = new(_key, TagSize))
            aes.Decrypt(nonce, ct, tag, pt, ad);
        return Encoding.UTF8.GetString(pt);
    }
}
