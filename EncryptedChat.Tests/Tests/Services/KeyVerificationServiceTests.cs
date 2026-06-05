using System.Collections.Concurrent;
using EncryptedChat.Client.Services.Crypto;
using FluentAssertions;
using Xunit;

namespace EncryptedChat.Tests.Tests.Services;

public class KeyVerificationServiceTests
{
    private sealed class InMemoryPinStore : IPinStore
    {
        private readonly ConcurrentDictionary<string, PinRecord> _d = new();
        public Task<PinRecord?> GetAsync(string userId) =>
            Task.FromResult(_d.TryGetValue(userId, out var r) ? r : null);
        public Task SetAsync(string userId, PinRecord record)
        {
            _d[userId] = record;
            return Task.CompletedTask;
        }
    }

    private static KeyVerificationService NewService() => new(new InMemoryPinStore());

    [Fact]
    public async Task FirstSight_Pins()
    {
        var svc = NewService();
        (await svc.CheckAndPinAsync("u", "s", "e")).Should().Be(KeyPinResult.Pinned);
    }

    [Fact]
    public async Task SameKeys_Matches()
    {
        var svc = NewService();
        await svc.CheckAndPinAsync("u", "s", "e");
        (await svc.CheckAndPinAsync("u", "s", "e")).Should().Be(KeyPinResult.Matches);
    }

    [Fact]
    public async Task DifferentKeys_Changed_AndDoesNotOverwrite()
    {
        var svc = NewService();
        await svc.CheckAndPinAsync("u", "s", "e");
        (await svc.CheckAndPinAsync("u", "s2", "e")).Should().Be(KeyPinResult.Changed);
        (await svc.CheckAndPinAsync("u", "s", "e")).Should().Be(KeyPinResult.Matches);
    }

    [Fact]
    public async Task MarkVerified_SetsStatus()
    {
        var svc = NewService();
        await svc.CheckAndPinAsync("u", "s", "e");
        await svc.MarkVerifiedAsync("u");
        (await svc.GetStatusAsync("u"))!.Status.Should().Be("verified");
    }

    [Fact]
    public async Task TrustNewKey_RepinsAndResetsToPinned()
    {
        var svc = NewService();
        await svc.CheckAndPinAsync("u", "s", "e");
        await svc.MarkVerifiedAsync("u");
        await svc.TrustNewKeyAsync("u", "s2", "e");
        var st = await svc.GetStatusAsync("u");
        st!.Status.Should().Be("pinned");
        (await svc.CheckAndPinAsync("u", "s2", "e")).Should().Be(KeyPinResult.Matches);
    }
}
