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
    EncryptedChatContext context,
    ISessionService sessionService) : IAuthService
{
    private const string DefaultUserRole = "User";

    private readonly UserManager<User> _userManager = userManager;
    private readonly SignInManager<User> _signInManager = signInManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly JwtTokenService _tokens = tokens;
    private readonly EncryptedChatContext _context = context;
    private readonly ISessionService _sessionService = sessionService;

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

    public async Task<LoginResult> LoginAsync(LoginDTO model, string? deviceInfo = null, string? deviceKind = null, string? ipAddress = null)
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

        if (!string.IsNullOrEmpty(deviceInfo))
        {
            // Check for existing session with same device to avoid duplicates
            var existingSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.DeviceInfo == deviceInfo && !s.IsRevoked);

            if (existingSession != null)
            {
                // Update existing session — re-point to the freshly issued refresh token
                existingSession.TokenHash = SessionService.HashToken(tokenPair.AccessToken);
                existingSession.LastActiveAt = DateTime.UtcNow;
                existingSession.CurrentRefreshTokenId = refreshTokenEntity.Id;
            }
            else
            {
                // Create new session linked to the new refresh token
                await _sessionService.CreateSessionAsync(
                    user.Id,
                    tokenPair.AccessToken,
                    deviceInfo,
                    deviceKind ?? "web",
                    ipAddress,
                    refreshTokenEntity.Id
                );
            }
        }

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

        if (token == null)
            return;

        token.RevokedAt = DateTime.UtcNow;

        // Revoke the session linked to this refresh token so it disappears
        // from the user's Active Sessions panel immediately.
        Session? linkedSession = await _context.Sessions
            .FirstOrDefaultAsync(s => s.CurrentRefreshTokenId == token.Id && !s.IsRevoked);
        if (linkedSession != null)
        {
            linkedSession.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken, string? oldAccessToken = null, string? deviceInfo = null, string? deviceKind = null, string? ipAddress = null)
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

        // Update session token hash
        string newTokenHash = SessionService.HashToken(newTokenPair.AccessToken);
        Session? session = null;

        // First try to find session by old token hash
        if (!string.IsNullOrEmpty(oldAccessToken))
        {
            string oldTokenHash = SessionService.HashToken(oldAccessToken);
            session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.TokenHash == oldTokenHash && !s.IsRevoked);
        }

        // If not found by token, find existing session for same device (avoid duplicates)
        if (session == null && !string.IsNullOrEmpty(deviceInfo))
        {
            session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.DeviceInfo == deviceInfo && !s.IsRevoked);
        }

        if (session != null)
        {
            // Update existing session — re-point to the rotated refresh token
            session.TokenHash = newTokenHash;
            session.LastActiveAt = DateTime.UtcNow;
            session.CurrentRefreshTokenId = newRefreshToken.Id;
        }
        else if (!string.IsNullOrEmpty(deviceInfo))
        {
            // Create new session only if no matching session exists
            await _sessionService.CreateSessionAsync(
                user.Id,
                newTokenPair.AccessToken,
                deviceInfo,
                deviceKind ?? "web",
                ipAddress,
                newRefreshToken.Id
            );
        }

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

    public async Task<IdentityResult> ChangePasswordAsync(string userId, ChangePasswordDTO model)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "User not found"
            });

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            user.PasswordChangedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        return result;
    }

    public async Task<DateTime?> GetPasswordChangedAtAsync(string userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        return user?.PasswordChangedAt;
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
