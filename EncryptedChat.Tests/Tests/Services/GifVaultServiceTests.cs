using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Tests;

public sealed class GifVaultServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly GifVaultService _service;

    public GifVaultServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new EncryptedChatContext(options);
        _service = new GifVaultService(_context);
    }

    public void Dispose() => _context.Dispose();

    private static GifVaultWriteDTO Write(string blob = "blob", int expected = 0)
        => new("wrapped", "iv", blob, expected);

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoVault()
    {
        var result = await _service.GetAsync("alice", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_CreatesAtRevision1_WhenExpectedZero()
    {
        var result = await _service.UpsertAsync("alice", Write(expected: 0), CancellationToken.None);

        result.Kind.Should().Be(GifVaultUpsertKind.Ok);
        result.Revision.Should().Be(1);

        var stored = await _service.GetAsync("alice", CancellationToken.None);
        stored!.Revision.Should().Be(1);
        stored.Blob.Should().Be("blob");
    }

    [Fact]
    public async Task UpsertAsync_Conflicts_WhenCreatingWithNonZeroExpected()
    {
        var result = await _service.UpsertAsync("alice", Write(expected: 5), CancellationToken.None);

        result.Kind.Should().Be(GifVaultUpsertKind.Conflict);
        result.Revision.Should().Be(0);
        (await _service.GetAsync("alice", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_IncrementsRevision_OnMatchingUpdate()
    {
        await _service.UpsertAsync("alice", Write("v1", 0), CancellationToken.None);

        var result = await _service.UpsertAsync("alice", Write("v2", 1), CancellationToken.None);

        result.Kind.Should().Be(GifVaultUpsertKind.Ok);
        result.Revision.Should().Be(2);
        (await _service.GetAsync("alice", CancellationToken.None))!.Blob.Should().Be("v2");
    }

    [Fact]
    public async Task UpsertAsync_Conflicts_OnStaleRevision_AndDoesNotWrite()
    {
        await _service.UpsertAsync("alice", Write("v1", 0), CancellationToken.None); // rev 1

        var result = await _service.UpsertAsync("alice", Write("stale", 0), CancellationToken.None);

        result.Kind.Should().Be(GifVaultUpsertKind.Conflict);
        result.Revision.Should().Be(1); // current server revision
        (await _service.GetAsync("alice", CancellationToken.None))!.Blob.Should().Be("v1");
    }

    [Fact]
    public async Task UpsertAsync_IsolatesVaultsPerUser()
    {
        await _service.UpsertAsync("alice", Write("alice-blob", 0), CancellationToken.None);
        await _service.UpsertAsync("bob", Write("bob-blob", 0), CancellationToken.None);

        (await _service.GetAsync("alice", CancellationToken.None))!.Blob.Should().Be("alice-blob");
        (await _service.GetAsync("bob", CancellationToken.None))!.Blob.Should().Be("bob-blob");
    }
}
