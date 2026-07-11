using EncryptedChat.Client.Models;
using FluentAssertions;

namespace EncryptedChat.Tests.Tests.Models;

public class FriendQrPayloadCodecTests
{
    [Theory]
    [InlineData("alice_42", "alice_42")]
    [InlineData("@Alice", "Alice")]
    public void TryDecode_AcceptsPlainHandles(string payload, string expected)
    {
        bool success = FriendQrPayloadCodec.TryDecode(payload, out string handle);

        success.Should().BeTrue();
        handle.Should().Be(expected);
    }

    [Fact]
    public void EncodeAndDecode_RoundTripsVersionedPayload()
    {
        string payload = FriendQrPayloadCodec.Encode("alice_42");

        bool success = FriendQrPayloadCodec.TryDecode(payload, out string handle);

        success.Should().BeTrue();
        handle.Should().Be("alice_42");
        payload.Should().Contain("\"type\":\"encryptedchat.friend\"");
        payload.Should().Contain("\"version\":1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("alice@example.com")]
    [InlineData("https://attacker.example/friend/alice")]
    [InlineData("{\"type\":\"other\",\"version\":1,\"handle\":\"alice\"}")]
    [InlineData("{\"type\":\"encryptedchat.friend\",\"version\":2,\"handle\":\"alice\"}")]
    [InlineData("{\"type\":\"encryptedchat.friend\",\"version\":1,\"handle\":\"a!ice\"}")]
    public void TryDecode_RejectsUntrustedOrMalformedPayloads(string payload)
    {
        bool success = FriendQrPayloadCodec.TryDecode(payload, out string handle);

        success.Should().BeFalse();
        handle.Should().BeEmpty();
    }

    [Fact]
    public void TryDecode_RejectsOversizedPayloadBeforeParsing()
    {
        string payload = new('a', 513);

        bool success = FriendQrPayloadCodec.TryDecode(payload, out string handle);

        success.Should().BeFalse();
        handle.Should().BeEmpty();
    }
}
