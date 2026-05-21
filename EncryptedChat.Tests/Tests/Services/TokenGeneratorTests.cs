using EncryptedChat.Services;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class TokenGeneratorTests
{
    [Fact]
    public void Generate_Default_Returns10Chars()
    {
        var token = TokenGenerator.Generate();
        token.Should().HaveLength(10);
    }

    [Fact]
    public void Generate_CustomLength_HonorsLength()
    {
        var token = TokenGenerator.Generate(16);
        token.Should().HaveLength(16);
    }

    [Fact]
    public void Generate_OnlyAlphabetChars()
    {
        const string allowed = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        for (int i = 0; i < 50; i++)
        {
            var token = TokenGenerator.Generate(12);
            token.All(c => allowed.Contains(c)).Should().BeTrue("token chars must be in allowed alphabet");
        }
    }

    [Fact]
    public void Generate_DifferentEachCall()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => TokenGenerator.Generate()).ToHashSet();
        tokens.Should().HaveCount(100, "100 random tokens should collide essentially never at 53^10 entropy");
    }
}
