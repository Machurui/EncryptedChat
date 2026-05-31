using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EncryptedChat.Tests;

public sealed class UserKeysServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly UserManager<User> _userManager;
    private readonly UserKeysService _service;

    public UserKeysServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new EncryptedChatContext(options);
        _userManager = CreateUserManager(_context);
        _service = new UserKeysService(_context, _userManager);
    }

    public void Dispose()
    {
        _userManager.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task GetMyKeysAsync_ReturnsNull_WhenUserHasNoKeysYet()
    {
        await SeedUserAsync("u1");

        var keys = await _service.GetMyKeysAsync("u1");

        keys.Should().BeNull();
    }

    [Fact]
    public async Task SetMyKeysAsync_PersistsAllFourFields()
    {
        await SeedUserAsync("u2");

        var dto = new SetEncryptionKeysDTO(
            SigningPublicKey: "spkBase64",
            EncryptionPublicKey: "epkBase64",
            EncryptedKeyBundle: "bundleBase64",
            KeyBundleSalt: "saltBase64");

        bool ok = await _service.SetMyKeysAsync("u2", dto);
        ok.Should().BeTrue();

        var read = await _service.GetMyKeysAsync("u2");
        read.Should().NotBeNull();
        read!.SigningPublicKey.Should().Be("spkBase64");
        read.EncryptionPublicKey.Should().Be("epkBase64");
        read.EncryptedKeyBundle.Should().Be("bundleBase64");
        read.KeyBundleSalt.Should().Be("saltBase64");
    }

    [Fact]
    public async Task GetPublicKeysAsync_ReturnsBothPubkeys_AfterSet()
    {
        await SeedUserAsync("u3");
        await _service.SetMyKeysAsync("u3", new SetEncryptionKeysDTO("spk", "epk", "bundle", "salt"));

        var pub = await _service.GetPublicKeysAsync("u3");

        pub.Should().NotBeNull();
        pub!.SigningPublicKey.Should().Be("spk");
        pub.EncryptionPublicKey.Should().Be("epk");
    }

    [Fact]
    public async Task GetPublicKeysAsync_ReturnsNull_BeforeSet()
    {
        await SeedUserAsync("u4");

        var pub = await _service.GetPublicKeysAsync("u4");

        pub.Should().BeNull();
    }

    private async Task SeedUserAsync(string id)
    {
        User user = new()
        {
            Id = id,
            Email = $"{id}@test.com",
            NormalizedEmail = $"{id.ToUpperInvariant()}@TEST.COM",
            UserName = $"{id}@test.com",
            NormalizedUserName = $"{id.ToUpperInvariant()}@TEST.COM",
            Name = id,
            Handle = id,
            Level = 1
        };
        await _userManager.CreateAsync(user);
    }

    private static UserManager<User> CreateUserManager(EncryptedChatContext context)
    {
        UserStore<User> store = new(context);
        return new UserManager<User>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<User>>.Instance);
    }
}
