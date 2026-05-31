using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace EncryptedChat.Client.Services.Crypto;

public class CryptoService
{
    private const int Pbkdf2Iterations = 600_000;
    private const int SaltSizeBytes = 16;
    private const int AesKeySize = 32;          // 256-bit
    private const int AesIvSize = 12;           // 96-bit, standard for GCM
    private const int AesTagSize = 16;          // 128-bit, standard for GCM

    // ---------- Identity key generation ----------

    public record IdentityKeyPair(
        byte[] SigningPrivateKey,    // PKCS#8
        byte[] SigningPublicKey,     // SPKI
        byte[] EncryptionPrivateKey, // PKCS#8
        byte[] EncryptionPublicKey); // SPKI

    public IdentityKeyPair GenerateIdentityKeyPair()
    {
        using var signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var encryption = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        return new IdentityKeyPair(
            SigningPrivateKey: signing.ExportPkcs8PrivateKey(),
            SigningPublicKey: signing.ExportSubjectPublicKeyInfo(),
            EncryptionPrivateKey: encryption.ExportPkcs8PrivateKey(),
            EncryptionPublicKey: encryption.ExportSubjectPublicKeyInfo());
    }

    // ---------- Team secret ----------

    public byte[] GenerateTeamSecret() => RandomNumberGenerator.GetBytes(AesKeySize);

    // ---------- AES-GCM ----------

    public record AesGcmCiphertext(byte[] Iv, byte[] CiphertextWithTag);

    public AesGcmCiphertext EncryptAesGcm(byte[] plaintext, byte[] key)
    {
        byte[] iv = RandomNumberGenerator.GetBytes(AesIvSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AesTagSize];

        using var aes = new AesGcm(key, AesTagSize);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        byte[] ciphertextWithTag = new byte[ciphertext.Length + AesTagSize];
        Buffer.BlockCopy(ciphertext, 0, ciphertextWithTag, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, ciphertextWithTag, ciphertext.Length, AesTagSize);

        return new AesGcmCiphertext(iv, ciphertextWithTag);
    }

    public byte[] DecryptAesGcm(byte[] iv, byte[] ciphertextWithTag, byte[] key)
    {
        int ciphertextLen = ciphertextWithTag.Length - AesTagSize;
        byte[] ciphertext = new byte[ciphertextLen];
        byte[] tag = new byte[AesTagSize];
        Buffer.BlockCopy(ciphertextWithTag, 0, ciphertext, 0, ciphertextLen);
        Buffer.BlockCopy(ciphertextWithTag, ciphertextLen, tag, 0, AesTagSize);

        byte[] plaintext = new byte[ciphertextLen];
        using var aes = new AesGcm(key, AesTagSize);
        aes.Decrypt(iv, ciphertext, tag, plaintext);
        return plaintext;
    }

    // ---------- ECDSA sign/verify ----------

    public byte[] Sign(byte[] data, byte[] signingPrivateKeyPkcs8)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(signingPrivateKeyPkcs8, out _);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    public bool Verify(byte[] data, byte[] signature, byte[] signingPublicKeySpki)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(signingPublicKeySpki, out _);
        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }

    // ---------- ECIES-P256 wrap/unwrap (for Team.Secret per recipient) ----------

    public byte[] WrapKey(byte[] keyToWrap, byte[] recipientEncryptionPublicKeySpki)
    {
        using var recipient = ECDiffieHellman.Create();
        recipient.ImportSubjectPublicKeyInfo(recipientEncryptionPublicKeySpki, out _);

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[] ephemeralPubSpki = ephemeral.ExportSubjectPublicKeyInfo();

        // ECDH shared secret, then HKDF to derive the wrap-key.
        byte[] sharedSecret = ephemeral.DeriveKeyMaterial(recipient.PublicKey);
        byte[] wrapKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: sharedSecret,
            outputLength: AesKeySize,
            salt: null,
            info: Encoding.UTF8.GetBytes("EncryptedChat:TeamKeyWrap:v1"));

        AesGcmCiphertext wrapped = EncryptAesGcm(keyToWrap, wrapKey);

        // Output layout: 2-byte BE length || ephemeralPubSpki || iv || ciphertext+tag
        byte[] result = new byte[2 + ephemeralPubSpki.Length + wrapped.Iv.Length + wrapped.CiphertextWithTag.Length];
        result[0] = (byte)((ephemeralPubSpki.Length >> 8) & 0xFF);
        result[1] = (byte)(ephemeralPubSpki.Length & 0xFF);
        Buffer.BlockCopy(ephemeralPubSpki, 0, result, 2, ephemeralPubSpki.Length);
        Buffer.BlockCopy(wrapped.Iv, 0, result, 2 + ephemeralPubSpki.Length, wrapped.Iv.Length);
        Buffer.BlockCopy(wrapped.CiphertextWithTag, 0, result, 2 + ephemeralPubSpki.Length + wrapped.Iv.Length, wrapped.CiphertextWithTag.Length);

        return result;
    }

    public byte[] UnwrapKey(byte[] wrapped, byte[] recipientEncryptionPrivateKeyPkcs8)
    {
        int ephemeralLen = (wrapped[0] << 8) | wrapped[1];
        byte[] ephemeralSpki = new byte[ephemeralLen];
        Buffer.BlockCopy(wrapped, 2, ephemeralSpki, 0, ephemeralLen);

        int ivOffset = 2 + ephemeralLen;
        byte[] iv = new byte[AesIvSize];
        Buffer.BlockCopy(wrapped, ivOffset, iv, 0, AesIvSize);

        int ciphertextOffset = ivOffset + AesIvSize;
        int ciphertextLen = wrapped.Length - ciphertextOffset;
        byte[] ciphertextWithTag = new byte[ciphertextLen];
        Buffer.BlockCopy(wrapped, ciphertextOffset, ciphertextWithTag, 0, ciphertextLen);

        using var recipient = ECDiffieHellman.Create();
        recipient.ImportPkcs8PrivateKey(recipientEncryptionPrivateKeyPkcs8, out _);
        using var ephemeral = ECDiffieHellman.Create();
        ephemeral.ImportSubjectPublicKeyInfo(ephemeralSpki, out _);

        byte[] sharedSecret = recipient.DeriveKeyMaterial(ephemeral.PublicKey);
        byte[] wrapKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: sharedSecret,
            outputLength: AesKeySize,
            salt: null,
            info: Encoding.UTF8.GetBytes("EncryptedChat:TeamKeyWrap:v1"));

        return DecryptAesGcm(iv, ciphertextWithTag, wrapKey);
    }

    // ---------- Phrase-derived wrap-key for identity bundle ----------

    public byte[] DeriveWrapKey(string phrase, byte[] salt)
    {
        return KeyDerivation.Pbkdf2(
            password: phrase,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Pbkdf2Iterations,
            numBytesRequested: AesKeySize);
    }

    public byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSizeBytes);

    // ---------- Wrap/unwrap the identity private keys for server-side backup ----------

    public byte[] WrapIdentityPrivateKeys(byte[] signingPrivPkcs8, byte[] encPrivPkcs8, byte[] wrapKey)
    {
        var bundle = new
        {
            sign = Convert.ToBase64String(signingPrivPkcs8),
            enc = Convert.ToBase64String(encPrivPkcs8)
        };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(bundle);
        AesGcmCiphertext wrapped = EncryptAesGcm(json, wrapKey);

        byte[] result = new byte[AesIvSize + wrapped.CiphertextWithTag.Length];
        Buffer.BlockCopy(wrapped.Iv, 0, result, 0, AesIvSize);
        Buffer.BlockCopy(wrapped.CiphertextWithTag, 0, result, AesIvSize, wrapped.CiphertextWithTag.Length);
        return result;
    }

    public (byte[] SigningPrivateKey, byte[] EncryptionPrivateKey) UnwrapIdentityPrivateKeys(byte[] wrappedBundle, byte[] wrapKey)
    {
        byte[] iv = new byte[AesIvSize];
        Buffer.BlockCopy(wrappedBundle, 0, iv, 0, AesIvSize);
        byte[] ciphertext = new byte[wrappedBundle.Length - AesIvSize];
        Buffer.BlockCopy(wrappedBundle, AesIvSize, ciphertext, 0, ciphertext.Length);

        byte[] json = DecryptAesGcm(iv, ciphertext, wrapKey);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            Convert.FromBase64String(root.GetProperty("sign").GetString()!),
            Convert.FromBase64String(root.GetProperty("enc").GetString()!));
    }

    // ---------- Message envelope helpers ----------

    public record MessageEnvelope(string EncryptedText, string Iv, string Signature, int KeyGeneration);

    public MessageEnvelope EncryptAndSignMessage(
        string plaintext,
        byte[] teamSecret,
        int keyGeneration,
        byte[] signingPrivateKey,
        Guid teamId,
        string senderId)
    {
        AesGcmCiphertext encrypted = EncryptAesGcm(Encoding.UTF8.GetBytes(plaintext), teamSecret);

        byte[] sigInput = BuildSignatureInput(
            encrypted.CiphertextWithTag,
            encrypted.Iv,
            teamId,
            senderId,
            keyGeneration);
        byte[] sig = Sign(sigInput, signingPrivateKey);

        return new MessageEnvelope(
            EncryptedText: Convert.ToBase64String(encrypted.CiphertextWithTag),
            Iv: Convert.ToBase64String(encrypted.Iv),
            Signature: Convert.ToBase64String(sig),
            KeyGeneration: keyGeneration);
    }

    public string DecryptAndVerifyMessage(
        MessageEnvelope envelope,
        byte[] teamSecret,
        byte[] senderSigningPublicKey,
        Guid teamId,
        string senderId)
    {
        byte[] ciphertext = Convert.FromBase64String(envelope.EncryptedText);
        byte[] iv = Convert.FromBase64String(envelope.Iv);
        byte[] sig = Convert.FromBase64String(envelope.Signature);

        byte[] sigInput = BuildSignatureInput(ciphertext, iv, teamId, senderId, envelope.KeyGeneration);
        if (!Verify(sigInput, sig, senderSigningPublicKey))
            throw new InvalidOperationException("Message signature verification failed");

        return Encoding.UTF8.GetString(DecryptAesGcm(iv, ciphertext, teamSecret));
    }

    private static byte[] BuildSignatureInput(byte[] ciphertext, byte[] iv, Guid teamId, string senderId, int keyGen)
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

        return SHA256.HashData(toHash);
    }
}
