using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace EncryptedChat.Services;

public class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    RoleManager<IdentityRole> roleManager,
    JwtTokenService tokens,
    EncryptedChatContext context) : IAuthService
{
    private const string DefaultUserRole = "User";

    private readonly UserManager<User> _userManager = userManager;
    private readonly SignInManager<User> _signInManager = signInManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly JwtTokenService _tokens = tokens;
    private readonly EncryptedChatContext _context = context;

    public async Task<IdentityResult> RegisterAsync(RegisterDTO model)
    {
        User? existingByEmail = await _userManager.FindByEmailAsync(model.Email);
        if (existingByEmail != null)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateEmail",
                Description = "Email already in use"
            });

        bool nameExists = await _userManager.Users.AnyAsync(u => u.Name == model.Name);
        if (nameExists)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateName",
                Description = "Name already in use"
            });

        User user = new()
        {
            UserName = model.Email,
            Name = model.Name,
            Email = model.Email,
            Level = 1,
            Secret = Guid.NewGuid().ToString("N")
        };

        IdentityResult result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            bool roleExists = await _roleManager.RoleExistsAsync(DefaultUserRole);
            if (!roleExists)
            {
                await _userManager.DeleteAsync(user);
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RoleMissing",
                    Description = $"Required role '{DefaultUserRole}' is not configured."
                });
            }

            IdentityResult roleResult = await _userManager.AddToRoleAsync(user, DefaultUserRole);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return roleResult;
            }
        }

        return result;
    }

    public async Task<LoginResult> LoginAsync(LoginDTO model)
    {
        User? user = await _userManager.FindByEmailAsync(model.Email)
                     ?? await _userManager.FindByNameAsync(model.Email);

        if (user is null)
            return LoginResult.Fail("Invalid credentials");

        SignInResult signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
            return LoginResult.Fail("Invalid credentials");

        IList<string> roles = await _userManager.GetRolesAsync(user);

        JwtTokenService.TokenPair tokenPair = _tokens.CreateTokenPair(user, roles);

        RefreshToken refreshTokenEntity = new()
        {
            Id = Guid.NewGuid(),
            Token = HashRefreshToken(tokenPair.RefreshToken),
            UserId = user.Id,
            ExpiresAt = tokenPair.RefreshTokenExpiresUtc,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return LoginResult.Success(tokenPair.AccessToken, tokenPair.AccessTokenExpiresUtc, tokenPair.RefreshToken);
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        string refreshTokenHash = HashRefreshToken(refreshToken);
        RefreshToken? token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenHash);

        if (token != null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken)
    {
        string refreshTokenHash = HashRefreshToken(refreshToken);
        RefreshToken? storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenHash);

        if (storedToken is null || !storedToken.IsActive || storedToken.User is null)
            return LoginResult.Fail("Invalid refresh token");

        storedToken.RevokedAt = DateTime.UtcNow;

        User user = storedToken.User;
        IList<string> roles = await _userManager.GetRolesAsync(user);

        JwtTokenService.TokenPair newTokenPair = _tokens.CreateTokenPair(user, roles);

        RefreshToken newRefreshToken = new()
        {
            Id = Guid.NewGuid(),
            Token = HashRefreshToken(newTokenPair.RefreshToken),
            UserId = user.Id,
            ExpiresAt = newTokenPair.RefreshTokenExpiresUtc,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        return LoginResult.Success(newTokenPair.AccessToken, newTokenPair.AccessTokenExpiresUtc, newTokenPair.RefreshToken);
    }

    public async Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model)
    {
        throw new NotImplementedException();
    }

    public async Task<IdentityResult> ResetPasswordAsync(ResetPasswordDTO model)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> ResendConfirmationEmailAsync(ResendConfirmationEmailDTO model)
    {
        throw new NotImplementedException();
    }

    private static string HashRefreshToken(string refreshToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}

public record LoginResult(bool Succeeded, string? AccessToken, DateTime? ExpiresUtc, string? RefreshToken, string? Error)
{
    public static LoginResult Success(string token, DateTime expiresUtc, string? refresh = null)
        => new(true, token, expiresUtc, refresh, null);

    public static LoginResult Fail(string error)
        => new(false, null, null, null, error);
}
