using EncryptedChat.Client.Services.Crypto;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class SafetyNumberTests
{
    private const string User = "user-123";
    private const string Sign = "c2lnbmtleQ==";   // "signkey"
    private const string Enc = "ZW5ja2V5";          // "enckey"

    [Fact]
    public void Compute_IsDeterministic()
    {
        var a = SafetyNumber.Compute(User, Sign, Enc);
        var b = SafetyNumber.Compute(User, Sign, Enc);
        a.Should().Be(b);
    }

    [Fact]
    public void Compute_Format_Is12GroupsOf5Digits()
    {
        var sn = SafetyNumber.Compute(User, Sign, Enc);
        var groups = sn.Split(' ');
        groups.Should().HaveCount(12);
        groups.Should().OnlyContain(g => g.Length == 5 && g.All(char.IsDigit));
    }

    [Fact]
    public void Compute_ChangesWhenSigningKeyChanges()
    {
        SafetyNumber.Compute(User, Sign, Enc)
            .Should().NotBe(SafetyNumber.Compute(User, "b3RoZXI=", Enc));
    }

    [Fact]
    public void Compute_ChangesWhenEncryptionKeyChanges()
    {
        SafetyNumber.Compute(User, Sign, Enc)
            .Should().NotBe(SafetyNumber.Compute(User, Sign, "b3RoZXI="));
    }

    [Fact]
    public void Compute_ChangesWhenUserIdChanges()
    {
        SafetyNumber.Compute(User, Sign, Enc)
            .Should().NotBe(SafetyNumber.Compute("user-999", Sign, Enc));
    }
}
