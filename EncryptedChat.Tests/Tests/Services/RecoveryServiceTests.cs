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

public sealed class RecoveryServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RecoveryService _service;

    public RecoveryServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new EncryptedChatContext(options);

        UserStore<User> store = new(_context);
        _userManager = new UserManager<User>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        _service = new RecoveryService(_context, _userManager);
    }

    public void Dispose()
    {
        _userManager.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task GenerateRecoveryPhraseAsync_ReturnsTwelveBip39Words_AndStoresHashAndSalt()
    {
        await SeedUserAsync("user-1");

        var phrase = await _service.GenerateRecoveryPhraseAsync("user-1");

        phrase.Should().NotBeNull();
        phrase!.Words.Should().HaveCount(12);
        phrase.Words.Should().OnlyContain(w => Bip39Words.All.Contains(w));

        User reloaded = await _context.Users.SingleAsync(u => u.Id == "user-1");
        reloaded.RecoveryPhraseHash.Should().NotBeNullOrEmpty();
        reloaded.RecoveryPhraseSalt.Should().NotBeNullOrEmpty();
        reloaded.RecoveryPhraseLastViewed.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyRecoveryPhraseAsync_TrueForCorrectPhrase()
    {
        await SeedUserAsync("user-2");
        var phrase = await _service.GenerateRecoveryPhraseAsync("user-2");

        bool valid = await _service.VerifyRecoveryPhraseAsync("user-2", phrase!.Words);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyRecoveryPhraseAsync_FalseForWrongPhrase()
    {
        await SeedUserAsync("user-3");
        await _service.GenerateRecoveryPhraseAsync("user-3");

        var wrong = Enumerable.Range(0, 12).Select(_ => "abandon").ToList();
        bool valid = await _service.VerifyRecoveryPhraseAsync("user-3", wrong);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyRecoveryPhraseAsync_FalseWhenNoPhraseGenerated()
    {
        await SeedUserAsync("user-4");
        var anyWords = Bip39Words.All.Take(12).ToList();

        bool valid = await _service.VerifyRecoveryPhraseAsync("user-4", anyWords);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyRecoveryPhraseAsync_FalseForWrongLength()
    {
        await SeedUserAsync("user-5");
        var phrase = await _service.GenerateRecoveryPhraseAsync("user-5");

        bool valid = await _service.VerifyRecoveryPhraseAsync("user-5", phrase!.Words.Take(10).ToList());

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateRecoveryPhraseAsync_RegeneratingInvalidatesOldPhrase()
    {
        await SeedUserAsync("user-6");
        var firstPhrase = await _service.GenerateRecoveryPhraseAsync("user-6");

        await _service.GenerateRecoveryPhraseAsync("user-6");

        bool oldStillValid = await _service.VerifyRecoveryPhraseAsync("user-6", firstPhrase!.Words);
        oldStillValid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyRecoveryPhraseAsync_TrueForCorrectPhrase_TolerantToCaseAndWhitespace()
    {
        await SeedUserAsync("user-7");
        var phrase = await _service.GenerateRecoveryPhraseAsync("user-7");

        var mutated = phrase!.Words
            .Select((w, i) => i % 2 == 0 ? $"  {w.ToUpperInvariant()}  " : w)
            .ToList();

        bool valid = await _service.VerifyRecoveryPhraseAsync("user-7", mutated);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyRecoveryPhraseAsync_PhraseOfOneUser_DoesNotVerifyAgainstAnotherUser()
    {
        await SeedUserAsync("user-8a");
        await SeedUserAsync("user-8b");

        var phraseA = await _service.GenerateRecoveryPhraseAsync("user-8a");
        await _service.GenerateRecoveryPhraseAsync("user-8b");

        bool aAgainstB = await _service.VerifyRecoveryPhraseAsync("user-8b", phraseA!.Words);

        aAgainstB.Should().BeFalse();
    }

    private async Task SeedUserAsync(string id)
    {
        User user = new()
        {
            Id = id,
            Email = $"{id}@test.com",
            NormalizedEmail = $"{id.ToUpper()}@TEST.COM",
            UserName = $"{id}@test.com",
            NormalizedUserName = $"{id.ToUpper()}@TEST.COM",
            Name = id,
            Handle = id.Replace("-", "_"),
            Level = 1,
            Secret = "s"
        };
        await _userManager.CreateAsync(user);
    }
}
