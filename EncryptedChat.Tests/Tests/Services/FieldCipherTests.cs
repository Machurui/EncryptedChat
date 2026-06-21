using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Tests;

public class FieldCipherTests
{
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private static FieldCipher Build(string? key = TestKey)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = key })
            .Build();
        return new FieldCipher(config);
    }

    [Fact]
    public void RoundTrip_ReturnsOriginal()
    {
        FieldCipher c = Build();
        string plain = "héllo 🌍 status";
        string? enc = c.Encrypt(plain, "Field");
        enc.Should().NotBeNull();
        enc.Should().NotBe(plain);
        c.Decrypt(enc, "Field").Should().Be(plain);
    }

    [Fact]
    public void Encrypt_UsesRandomNonce_DifferentCiphertextEachTime()
    {
        FieldCipher c = Build();
        c.Encrypt("same", "Field").Should().NotBe(c.Encrypt("same", "Field"));
    }

    [Fact]
    public void Decrypt_Throws_WhenCiphertextTampered()
    {
        FieldCipher c = Build();
        byte[] blob = Convert.FromBase64String(c.Encrypt("secret", "Field")!);
        blob[^1] ^= 0xFF; // flip last ciphertext byte (version + length unchanged)
        string tampered = Convert.ToBase64String(blob);
        Action act = () => c.Decrypt(tampered, "Field");
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmpty_Passthrough(string? value)
    {
        FieldCipher c = Build();
        c.Encrypt(value, "Field").Should().Be(value);
        c.Decrypt(value, "Field").Should().Be(value);
    }

    [Fact]
    public void Decrypt_LegacyPlaintext_ReturnedAsIs()
    {
        FieldCipher c = Build();
        c.Decrypt("just a plain status", "Field").Should().Be("just a plain status");
    }

    [Fact]
    public void Ctor_Throws_WhenKeyMissingOrWrongSize()
    {
        Action missing = () => Build(key: null);
        missing.Should().Throw<InvalidOperationException>();
        Action wrong = () => Build(key: Convert.ToBase64String(new byte[16]));
        wrong.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decrypt_Throws_WhenAadMismatch()
    {
        FieldCipher c = Build();
        string? enc = c.Encrypt("secret", "FieldA");
        Action act = () => c.Decrypt(enc, "FieldB");
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
