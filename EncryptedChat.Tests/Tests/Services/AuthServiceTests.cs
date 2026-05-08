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
        _service = new AuthService(
            _userManager,
            _signInManager.Object,
            _roleManager,
            new JwtTokenService(CreateConfiguration()),
            _context);
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
