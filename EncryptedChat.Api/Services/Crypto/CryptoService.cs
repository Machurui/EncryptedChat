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
