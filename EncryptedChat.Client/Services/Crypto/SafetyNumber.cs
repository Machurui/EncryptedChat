using System.Security.Cryptography;
using System.Text;

namespace EncryptedChat.Client.Services.Crypto;

public static class SafetyNumber
{
    private const int Iterations = 5200;   // key-stretching, Signal-style
    private const int Groups = 12;         // 12 groups of 5 digits = 60 digits
    private const string Version = "EC-SN-1";

    /// Deterministic human-comparable safety number for a user's public key pair.
    /// Covers BOTH public keys + the userId so swapping either is detected and the
    /// number is bound to the identity. Culture-invariant.
    public static string Compute(string userId, string signingPubB64, string encryptionPubB64)
    {
        byte[] input = Encoding.UTF8.GetBytes(
            $"{Version}|{userId}|{signingPubB64}|{encryptionPubB64}");

        byte[] h = SHA512.HashData(input);             // 64 bytes
        for (int i = 1; i < Iterations; i++)
        {
            byte[] buf = new byte[h.Length + input.Length];
            Buffer.BlockCopy(h, 0, buf, 0, h.Length);
            Buffer.BlockCopy(input, 0, buf, h.Length, input.Length);
            h = SHA512.HashData(buf);
        }

        var sb = new StringBuilder(Groups * 6);
        for (int g = 0; g < Groups; g++)
        {
            long v = 0;
            for (int b = 0; b < 5; b++) v = (v << 8) | h[g * 5 + b];
            sb.Append((v % 100000).ToString("D5"));
            if (g < Groups - 1) sb.Append(' ');
        }
        return sb.ToString();
    }
}
