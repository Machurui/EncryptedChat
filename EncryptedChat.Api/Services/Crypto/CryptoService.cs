using System.Security.Cryptography;
using System.Text;

namespace EncryptedChat.Services;

public class CryptoService : ICryptoService
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int Pbkdf2Iterations = 100000;
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("EncryptedChat.TeamKey.v1");

    public (string EncryptedText, string Iv) Encrypt(string plaintext, string teamSecret)
    {
        byte[] key = DeriveKey(teamSecret);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TagSizeBytes];

        using AesGcm aes = new(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        byte[] combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return (Convert.ToBase64String(combined), Convert.ToBase64String(nonce));
    }

    public string Decrypt(string encryptedText, string iv, string teamSecret)
    {
        byte[] key = DeriveKey(teamSecret);
        byte[] nonce = Convert.FromBase64String(iv);
        byte[] combined = Convert.FromBase64String(encryptedText);

        byte[] ciphertext = new byte[combined.Length - TagSizeBytes];
        byte[] tag = new byte[TagSizeBytes];
        Buffer.BlockCopy(combined, 0, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, ciphertext.Length, tag, 0, TagSizeBytes);

        byte[] plaintextBytes = new byte[ciphertext.Length];

        using AesGcm aes = new(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public string Sign(string plaintext, string userSecret)
    {
        byte[] key = Encoding.UTF8.GetBytes(userSecret);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(plaintextBytes);

        return Convert.ToBase64String(hash);
    }

    public bool Verify(string plaintext, string signature, string userSecret)
    {
        string computed = Sign(plaintext, userSecret);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computed),
            Convert.FromBase64String(signature));
    }

    public (byte[] EncryptedData, string Iv) EncryptBytes(byte[] plaintext, string secret)
    {
        byte[] key = DeriveKey(secret);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using AesGcm aes = new(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return (combined, Convert.ToBase64String(nonce));
    }

    public byte[] DecryptBytes(byte[] ciphertext, string iv, string secret)
    {
        byte[] key = DeriveKey(secret);
        byte[] nonce = Convert.FromBase64String(iv);

        byte[] encryptedData = new byte[ciphertext.Length - TagSizeBytes];
        byte[] tag = new byte[TagSizeBytes];
        Buffer.BlockCopy(ciphertext, 0, encryptedData, 0, encryptedData.Length);
        Buffer.BlockCopy(ciphertext, encryptedData.Length, tag, 0, TagSizeBytes);

        byte[] plaintext = new byte[encryptedData.Length];

        using AesGcm aes = new(key, TagSizeBytes);
        aes.Decrypt(nonce, encryptedData, tag, plaintext);

        return plaintext;
    }

    public string SignBytes(byte[] data, string userSecret)
    {
        byte[] key = Encoding.UTF8.GetBytes(userSecret);

        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(data);

        return Convert.ToBase64String(hash);
    }

    public bool VerifyBytes(byte[] data, string signature, string userSecret)
    {
        string computed = SignBytes(data, userSecret);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computed),
            Convert.FromBase64String(signature));
    }

    private static byte[] DeriveKey(string secret)
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        return Rfc2898DeriveBytes.Pbkdf2(
            secretBytes,
            Salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }
}
