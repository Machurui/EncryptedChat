using EncryptedChat.Data;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EncryptedChat.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly EncryptedChatContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly Mock<SignInManager<User>> _signInManager;
    private readonly Mock<ISessionService> _sessionService;
    private readonly Mock<IRecoveryService> _recoveryService = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        DbContextOptions<EncryptedChatContext> options = new DbContextOptionsBuilder<EncryptedChatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new EncryptedChatContext(options);
        _userManager = CreateUserManager(_context);
        _roleManager = CreateRoleManager(_context);
        _signInManager = CreateSignInManager(_userManager);
        _sessionService = new Mock<ISessionService>();
        _recoveryService.Setup(r => r.GenerateRecoveryPhraseAsync(It.IsAny<string>()))
            .ReturnsAsync(new RecoveryPhraseDTO(
                Bip39Words.All.Take(12).ToList(),
                DateTime.UtcNow));
        _service = new AuthService(
            _userManager,
            _signInManager.Object,
            _roleManager,
            new JwtTokenService(CreateConfiguration()),
            _context,
            _sessionService.Object,
            _recoveryService.Object);
    }

    public void Dispose()
    {
        _roleManager.Dispose();
        _userManager.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task LoginAsync_StoresHashedRefreshToken_AndRefreshAndLogoutUseRawToken()
    {
        User user = new()
        {
            Id = "user-1",
            Email = "user@test.com",
            NormalizedEmail = "USER@TEST.COM",
            UserName = "user@test.com",
            NormalizedUserName = "USER@TEST.COM",
            Name = "User",
            Level = 1,
            Secret = "secret"
        };
        await _userManager.CreateAsync(user);

        _signInManager
            .Setup(s => s.CheckPasswordSignInAsync(user, "P@ssw0rd", true))
            .ReturnsAsync(SignInResult.Success);

        LoginResult login = await _service.LoginAsync(new LoginDTO
        {
            Email = user.Email,
            Password = "P@ssw0rd"
        });

        login.Succeeded.Should().BeTrue();
        RefreshToken storedToken = await _context.RefreshTokens.SingleAsync();
        storedToken.Token.Should().NotBe(login.RefreshToken);

        LoginResult refreshed = await _service.RefreshAsync(login.RefreshToken!);
        refreshed.Succeeded.Should().BeTrue();
        storedToken.IsRevoked.Should().BeTrue();

        await _service.LogoutAsync(refreshed.RefreshToken);
        RefreshToken activeToken = await _context.RefreshTokens.SingleAsync(rt => rt.Token != storedToken.Token);
        activeToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task LoginRefreshLogout_SessionFkTracksCurrentRefreshToken_AndIsRevokedOnLogout()
    {
        User user = new()
        {
            Id = "user-fk",
            Email = "fk@test.com",
            NormalizedEmail = "FK@TEST.COM",
            UserName = "fk@test.com",
            NormalizedUserName = "FK@TEST.COM",
            Name = "FK User",
            Level = 1,
            Secret = "secret"
        };
        await _userManager.CreateAsync(user);

        _signInManager
            .Setup(s => s.CheckPasswordSignInAsync(user, "P@ssw0rd", true))
            .ReturnsAsync(SignInResult.Success);

        // The mock ISessionService doesn't create real Session rows, so seed
        // the existing-session row directly to exercise the update branch in
        // LoginAsync (and the same branch is taken on Refresh).
        Session seeded = new()
        {
            UserId = user.Id,
            TokenHash = "placeholder",
            DeviceInfo = "test-device",
            DeviceKind = "web"
        };
        _context.Sessions.Add(seeded);
        await _context.SaveChangesAsync();

        LoginResult login = await _service.LoginAsync(
            new LoginDTO { Email = user.Email, Password = "P@ssw0rd" },
            deviceInfo: "test-device",
            deviceKind: "web");
        login.Succeeded.Should().BeTrue();

        RefreshToken firstRefresh = await _context.RefreshTokens.SingleAsync();
        Session sessionAfterLogin = await _context.Sessions.SingleAsync();
        sessionAfterLogin.CurrentRefreshTokenId.Should().Be(firstRefresh.Id);

        LoginResult refreshed = await _service.RefreshAsync(
            login.RefreshToken!,
            deviceInfo: "test-device",
            deviceKind: "web");
        refreshed.Succeeded.Should().BeTrue();

        RefreshToken newRefresh = await _context.RefreshTokens
            .SingleAsync(rt => rt.RevokedAt == null);
        Session sessionAfterRefresh = await _context.Sessions.SingleAsync();
        sessionAfterRefresh.CurrentRefreshTokenId.Should().Be(newRefresh.Id,
            "the FK should rotate to the newly-issued refresh token");

        await _service.LogoutAsync(refreshed.RefreshToken);
        Session sessionAfterLogout = await _context.Sessions.SingleAsync();
        sessionAfterLogout.IsRevoked.Should().BeTrue(
            "logout should also revoke the linked session row");
    }

    [Fact]
    public async Task RegisterAsync_Succeeds_SetsHandleAndNameToHandle()
    {
        await _roleManager.CreateAsync(new IdentityRole("User"));

        var (result, recoveryWords) = await _service.RegisterAsync(new RegisterDTO
        {
            Email = "newuser@test.com",
            Password = "P@ssw0rd123",
            Handle = "NewUser"
        });

        result.Succeeded.Should().BeTrue();
        recoveryWords.Should().NotBeNull();
        recoveryWords!.Should().HaveCount(12);
        User created = await _userManager.Users.SingleAsync(u => u.Email == "newuser@test.com");
        created.Handle.Should().Be("newuser");
        created.Name.Should().Be("newuser");
    }

    [Fact]
    public async Task RegisterAsync_FailsWith_DuplicateHandle()
    {
        User existing = new()
        {
            Id = "user-existing",
            Email = "existing@test.com",
            NormalizedEmail = "EXISTING@TEST.COM",
            UserName = "existing@test.com",
            NormalizedUserName = "EXISTING@TEST.COM",
            Name = "existing",
            Handle = "taken",
            Level = 1,
            Secret = "secret"
        };
        await _userManager.CreateAsync(existing);

        var (result, recoveryWords) = await _service.RegisterAsync(new RegisterDTO
        {
            Email = "other@test.com",
            Password = "P@ssw0rd123",
            Handle = "Taken"
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "DuplicateHandle");
        recoveryWords.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_FailsWith_DuplicateEmail()
    {
        User existing = new()
        {
            Id = "user-existing-email",
            Email = "dup@test.com",
            NormalizedEmail = "DUP@TEST.COM",
            UserName = "dup@test.com",
            NormalizedUserName = "DUP@TEST.COM",
            Name = "first",
            Handle = "first",
            Level = 1,
            Secret = "secret"
        };
        await _userManager.CreateAsync(existing);

        var (result, recoveryWords) = await _service.RegisterAsync(new RegisterDTO
        {
            Email = "dup@test.com",
            Password = "P@ssw0rd123",
            Handle = "different"
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "DuplicateEmail");
        recoveryWords.Should().BeNull();
    }

    [Fact]
    public async Task RecoverAsync_ValidPhrase_ResetsPassword_AndReturnsNewWords()
    {
        await _roleManager.CreateAsync(new IdentityRole("User"));
        await SeedUserWithPasswordAsync("alice@test.com", "OldP@ssw0rd!", "alice");

        var realRecovery = new RecoveryService(_context, _userManager);
        var phrase = await realRecovery.GenerateRecoveryPhraseAsync(
            (await _userManager.FindByEmailAsync("alice@test.com"))!.Id);

        var service = new AuthService(
            _userManager,
            _signInManager.Object,
            _roleManager,
            new JwtTokenService(CreateConfiguration()),
            _context,
            _sessionService.Object,
            realRecovery);

        var (success, _, newWords) = await service.RecoverAsync(
            "alice@test.com",
            phrase!.Words.ToList(),
            "NewP@ssw0rd!");

        success.Should().BeTrue();
        newWords.Should().NotBeNull();
        newWords!.Should().HaveCount(12);

        var user = await _userManager.FindByEmailAsync("alice@test.com");
        (await _userManager.CheckPasswordAsync(user!, "NewP@ssw0rd!")).Should().BeTrue();
        (await _userManager.CheckPasswordAsync(user!, "OldP@ssw0rd!")).Should().BeFalse();

        (await realRecovery.VerifyRecoveryPhraseAsync(user!.Id, phrase.Words)).Should().BeFalse();
    }

    [Fact]
    public async Task RecoverAsync_UnknownEmail_ReturnsGenericFailure()
    {
        var realRecovery = new RecoveryService(_context, _userManager);
        var service = new AuthService(
            _userManager,
            _signInManager.Object,
            _roleManager,
            new JwtTokenService(CreateConfiguration()),
            _context,
            _sessionService.Object,
            realRecovery);

        var (success, message, newWords) = await service.RecoverAsync(
            "nobody@test.com",
            Bip39Words.All.Take(12).ToList(),
            "NewP@ssw0rd!");

        success.Should().BeFalse();
        message.Should().Be("Invalid email or recovery phrase");
        newWords.Should().BeNull();
    }

    [Fact]
    public async Task RecoverAsync_WrongPhrase_ReturnsGenericFailure()
    {
        await _roleManager.CreateAsync(new IdentityRole("User"));
        await SeedUserWithPasswordAsync("bob@test.com", "OldP@ssw0rd!", "bob");

        var realRecovery = new RecoveryService(_context, _userManager);
        await realRecovery.GenerateRecoveryPhraseAsync(
            (await _userManager.FindByEmailAsync("bob@test.com"))!.Id);

        var service = new AuthService(
            _userManager,
            _signInManager.Object,
            _roleManager,
            new JwtTokenService(CreateConfiguration()),
            _context,
            _sessionService.Object,
            realRecovery);

        var wrong = Enumerable.Range(0, 12).Select(_ => "abandon").ToList();
        var (success, message, _) = await service.RecoverAsync(
            "bob@test.com", wrong, "NewP@ssw0rd!");

        success.Should().BeFalse();
        message.Should().Be("Invalid email or recovery phrase");
    }

    private async Task SeedUserWithPasswordAsync(string email, string password, string handle)
    {
        User user = new()
        {
            Id = handle + "-id",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Name = handle,
            Handle = handle,
            Level = 1,
            Secret = "s"
        };
        await _userManager.CreateAsync(user, password);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "auth-service-test-jwt-key-with-at-least-32-bytes",
                ["Jwt:Issuer"] = "EncryptedChat.Tests",
                ["Jwt:Audience"] = "EncryptedChat.Tests"
            })
            .Build();

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

    private static RoleManager<IdentityRole> CreateRoleManager(EncryptedChatContext context)
    {
        RoleStore<IdentityRole> store = new(context);
        return new RoleManager<IdentityRole>(
            store,
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
    }

    private static Mock<SignInManager<User>> CreateSignInManager(UserManager<User> userManager)
    {
        return new Mock<SignInManager<User>>(
            userManager,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<User>>().Object,
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<User>>().Object);
    }
}
