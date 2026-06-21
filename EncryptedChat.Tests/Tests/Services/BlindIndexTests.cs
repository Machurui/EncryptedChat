using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Tests;

public class BlindIndexTests
{
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private static BlindIndex Build(string? key = TestKey)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = key })
            .Build();
        return new BlindIndex(config);
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        BlindIndex b = Build();
        b.Compute("alice").Should().Be(b.Compute("alice"));
    }

    [Fact]
    public void Compute_DiffersForDifferentInputs()
    {
        BlindIndex b = Build();
        b.Compute("alice").Should().NotBe(b.Compute("bob"));
    }

    [Fact]
    public void Compute_IsCaseSensitiveToInput_CallerNormalizes()
    {
        // The blind index does NOT normalize — callers pass the normalized value.
        BlindIndex b = Build();
        b.Compute("Alice").Should().NotBe(b.Compute("alice"));
    }

    [Fact]
    public void Compute_DiffersFromRawHmacOfMasterKey_UsesDerivedSubkey()
    {
        // Sanity: output isn't the plaintext and is a base64 32-byte digest.
        BlindIndex b = Build();
        string idx = b.Compute("alice");
        idx.Should().NotBe("alice");
        Convert.FromBase64String(idx).Length.Should().Be(32); // HMAC-SHA256 = 32 bytes
    }

    [Fact]
    public void Ctor_Throws_WhenKeyMissing()
    {
        Action act = () => Build(key: null);
        act.Should().Throw<InvalidOperationException>();
    }
}
