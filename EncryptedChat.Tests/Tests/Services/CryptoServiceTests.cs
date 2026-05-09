using EncryptedChat.Services;
using FluentAssertions;
using System.Security.Cryptography;

namespace EncryptedChat.Tests;

public class CryptoServiceTests
{
    private readonly CryptoService _crypto = new();

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_ReturnsOriginalText()
    {
        string plaintext = "Hello, encrypted world!";
        string teamSecret = Guid.NewGuid().ToString("N");

        (string encrypted, string iv) = _crypto.Encrypt(plaintext, teamSecret);
        string decrypted = _crypto.Decrypt(encrypted, iv, teamSecret);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_SameText_ProducesDifferentCiphertext()
    {
        string plaintext = "Same message";
        string teamSecret = Guid.NewGuid().ToString("N");

        (string encrypted1, string iv1) = _crypto.Encrypt(plaintext, teamSecret);
        (string encrypted2, string iv2) = _crypto.Encrypt(plaintext, teamSecret);

        encrypted1.Should().NotBe(encrypted2);
        iv1.Should().NotBe(iv2);
    }

    [Fact]
    public void Decrypt_WrongSecret_ThrowsCryptographicException()
    {
        string plaintext = "Secret message";
        string teamSecret = Guid.NewGuid().ToString("N");
        string wrongSecret = Guid.NewGuid().ToString("N");

        (string encrypted, string iv) = _crypto.Encrypt(plaintext, teamSecret);

        Action act = () => _crypto.Decrypt(encrypted, iv, wrongSecret);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        string plaintext = "Authentic message";
        string teamSecret = Guid.NewGuid().ToString("N");

        (string encrypted, string iv) = _crypto.Encrypt(plaintext, teamSecret);

        byte[] bytes = Convert.FromBase64String(encrypted);
        bytes[0] ^= 0xFF;
        string tampered = Convert.ToBase64String(bytes);

        Action act = () => _crypto.Decrypt(tampered, iv, teamSecret);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Sign_Verify_ValidSignature_ReturnsTrue()
    {
        string plaintext = "Message to sign";
        string userSecret = Guid.NewGuid().ToString("N");

        string signature = _crypto.Sign(plaintext, userSecret);
        bool isValid = _crypto.Verify(plaintext, signature, userSecret);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongSecret_ReturnsFalse()
    {
        string plaintext = "Message to sign";
        string userSecret = Guid.NewGuid().ToString("N");
        string wrongSecret = Guid.NewGuid().ToString("N");

        string signature = _crypto.Sign(plaintext, userSecret);
        bool isValid = _crypto.Verify(plaintext, signature, wrongSecret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedMessage_ReturnsFalse()
    {
        string plaintext = "Original message";
        string userSecret = Guid.NewGuid().ToString("N");

        string signature = _crypto.Sign(plaintext, userSecret);
        bool isValid = _crypto.Verify("Tampered message", signature, userSecret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void Encrypt_EmptyString_WorksCorrectly()
    {
        string plaintext = "";
        string teamSecret = Guid.NewGuid().ToString("N");

        (string encrypted, string iv) = _crypto.Encrypt(plaintext, teamSecret);
        string decrypted = _crypto.Decrypt(encrypted, iv, teamSecret);

        decrypted.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_UnicodeText_PreservesContent()
    {
        string plaintext = "Hello 世界 🌍 Привет";
        string teamSecret = Guid.NewGuid().ToString("N");

        (string encrypted, string iv) = _crypto.Encrypt(plaintext, teamSecret);
        string decrypted = _crypto.Decrypt(encrypted, iv, teamSecret);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_LargeMessage_WorksCorrectly()
    {
        string plaintext = new('A', 4000);
        string teamSecret = Guid.NewGuid().ToString("N");

        (string encrypted, string iv) = _crypto.Encrypt(plaintext, teamSecret);
        string decrypted = _crypto.Decrypt(encrypted, iv, teamSecret);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void EncryptBytes_DecryptBytes_RoundTrip_ReturnsOriginalData()
    {
        byte[] plaintext = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        string secret = Guid.NewGuid().ToString("N");

        (byte[] encrypted, string iv) = _crypto.EncryptBytes(plaintext, secret);
        byte[] decrypted = _crypto.DecryptBytes(encrypted, iv, secret);

        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void EncryptBytes_SameData_ProducesDifferentCiphertext()
    {
        byte[] plaintext = new byte[] { 0x01, 0x02, 0x03 };
        string secret = Guid.NewGuid().ToString("N");

        (byte[] encrypted1, string iv1) = _crypto.EncryptBytes(plaintext, secret);
        (byte[] encrypted2, string iv2) = _crypto.EncryptBytes(plaintext, secret);

        encrypted1.Should().NotBeEquivalentTo(encrypted2);
        iv1.Should().NotBe(iv2);
    }

    [Fact]
    public void DecryptBytes_WrongSecret_ThrowsCryptographicException()
    {
        byte[] plaintext = new byte[] { 0x01, 0x02, 0x03 };
        string secret = Guid.NewGuid().ToString("N");
        string wrongSecret = Guid.NewGuid().ToString("N");

        (byte[] encrypted, string iv) = _crypto.EncryptBytes(plaintext, secret);

        Action act = () => _crypto.DecryptBytes(encrypted, iv, wrongSecret);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptBytes_TamperedData_ThrowsCryptographicException()
    {
        byte[] plaintext = new byte[] { 0x01, 0x02, 0x03 };
        string secret = Guid.NewGuid().ToString("N");

        (byte[] encrypted, string iv) = _crypto.EncryptBytes(plaintext, secret);
        encrypted[0] ^= 0xFF;

        Action act = () => _crypto.DecryptBytes(encrypted, iv, secret);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void SignBytes_VerifyBytes_ValidSignature_ReturnsTrue()
    {
        byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        string userSecret = Guid.NewGuid().ToString("N");

        string signature = _crypto.SignBytes(data, userSecret);
        bool isValid = _crypto.VerifyBytes(data, signature, userSecret);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyBytes_WrongSecret_ReturnsFalse()
    {
        byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        string userSecret = Guid.NewGuid().ToString("N");
        string wrongSecret = Guid.NewGuid().ToString("N");

        string signature = _crypto.SignBytes(data, userSecret);
        bool isValid = _crypto.VerifyBytes(data, signature, wrongSecret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyBytes_TamperedData_ReturnsFalse()
    {
        byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        string userSecret = Guid.NewGuid().ToString("N");

        string signature = _crypto.SignBytes(data, userSecret);
        data[0] = 0xFF;
        bool isValid = _crypto.VerifyBytes(data, signature, userSecret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void EncryptBytes_LargeFile_WorksCorrectly()
    {
        byte[] plaintext = new byte[1024 * 1024];
        Random.Shared.NextBytes(plaintext);
        string secret = Guid.NewGuid().ToString("N");

        (byte[] encrypted, string iv) = _crypto.EncryptBytes(plaintext, secret);
        byte[] decrypted = _crypto.DecryptBytes(encrypted, iv, secret);

        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void EncryptBytes_EmptyData_WorksCorrectly()
    {
        byte[] plaintext = Array.Empty<byte>();
        string secret = Guid.NewGuid().ToString("N");

        (byte[] encrypted, string iv) = _crypto.EncryptBytes(plaintext, secret);
        byte[] decrypted = _crypto.DecryptBytes(encrypted, iv, secret);

        decrypted.Should().BeEmpty();
    }
}
