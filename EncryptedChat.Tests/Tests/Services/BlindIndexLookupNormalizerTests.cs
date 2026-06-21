using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Tests;

public class BlindIndexLookupNormalizerTests
{
    private const string TestKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private static BlindIndex BuildBlindIndex()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = TestKey })
            .Build();
        return new BlindIndex(config);
    }

    [Fact]
    public void NormalizeEmail_IsDeterministic_AndCaseInsensitive()
    {
        var n = new BlindIndexLookupNormalizer(BuildBlindIndex());
        n.NormalizeEmail("User@Test.com ").Should().Be(n.NormalizeEmail("user@test.com"));
    }

    [Fact]
    public void NormalizeEmail_Null_ReturnsNull()
    {
        new BlindIndexLookupNormalizer(BuildBlindIndex()).NormalizeEmail(null).Should().BeNull();
    }

    [Fact]
    public void NormalizeEmail_EqualsBlindIndex_IdentityPurpose()
    {
        BlindIndex bi = BuildBlindIndex();
        new BlindIndexLookupNormalizer(bi).NormalizeEmail("a@b.com")
            .Should().Be(bi.Compute("a@b.com", "identity"));
    }

    [Fact]
    public void NormalizeName_MatchesNormalizeEmail_ForSameValue()
    {
        var n = new BlindIndexLookupNormalizer(BuildBlindIndex());
        n.NormalizeName("a@b.com").Should().Be(n.NormalizeEmail("a@b.com"));
    }
}
