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
    ISessionService sessionService,
    IRecoveryService recoveryService,
    IPasswordHistoryService passwordHistory,
    IBlindIndex blindIndex) : IAuthService
{
    private const string DefaultUserRole = "User";
    private const string ReuseRejection = "You cannot reuse one of your last 3 passwords.";

    private readonly UserManager<User> _userManager = userManager;
    private readonly SignInManager<User> _signInManager = signInManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly JwtTokenService _tokens = tokens;
    private readonly EncryptedChatContext _context = context;
    private readonly ISessionService _sessionService = sessionService;
    private readonly IRecoveryService _recoveryService = recoveryService;
    private readonly IPasswordHistoryService _passwordHistory = passwordHistory;
    private readonly IBlindIndex _blindIndex = blindIndex;

    public async Task<(IdentityResult Result, IReadOnlyList<string>? RecoveryWords, string? AccessToken)> RegisterAsync(RegisterDTO model)
    {
        User? existingByEmail = await _userManager.FindByEmailAsync(model.Email);
        if (existingByEmail != null)
            return (IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateEmail",
                Description = "Email already in use"
            }), null, null);

        string handle = (model.Handle ?? string.Empty).Trim().ToLowerInvariant();
        string handleIndex = _blindIndex.Compute(handle);

        bool handleExists = await _userManager.Users.AnyAsync(u => u.HandleBlindIndex == handleIndex);
        if (handleExists)
            return (IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateHandle",
                Description = "Handle already in use"
            }), null, null);

        User user = new()
        {
            UserName = model.Email,
            Name = handle,    // initial display name = handle; user can customize later
            Handle = handle,
            HandleBlindIndex = handleIndex,
            Email = model.Email,
            Level = 1
        };

        IdentityResult result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return (result, null, null);

        bool roleExists = await _roleManager.RoleExistsAsync(DefaultUserRole);
        if (!roleExists)
        {
            await _userManager.DeleteAsync(user);
            return (IdentityResult.Failed(new IdentityError
            {
                Code = "RoleMissing",
                Description = $"Required role '{DefaultUserRole}' is not configured."
            }), null, null);
        }

        IdentityResult roleResult = await _userManager.AddToRoleAsync(user, DefaultUserRole);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return (roleResult, null, null);
        }

        RecoveryPhraseDTO? phrase = await _recoveryService.GenerateRecoveryPhraseAsync(user.Id);

        // Issue an access token immediately so the client can call
        // PUT /api/User/me/encryption-keys to upload the freshly-generated
        // identity key bundle without forcing the user to log in twice.
        // Also create the matching Session + RefreshToken — per-request session
        // validation (Program.cs OnTokenValidated → IsSessionValidAsync) would
        // otherwise reject this fresh JWT for lack of a Session row.
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
        await _sessionService.CreateSessionAsync(
            user.Id,
            tokenPair.AccessToken,
            deviceInfo: "Signup",
            deviceKind: "web",
            ipAddress: null,
            refreshTokenId: refreshTokenEntity.Id);
        await _context.SaveChangesAsync();

        return (result, phrase?.Words, tokenPair.AccessToken);
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

        // Block reuse of the last 3 passwords before invoking Identity's change
        // flow. Identity itself does not enforce this.
        if (await _passwordHistory.IsReusedAsync(user, model.NewPassword))
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRecentlyUsed",
                Description = ReuseRejection
            });

        string? previousHash = user.PasswordHash;

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(previousHash))
                await _passwordHistory.RecordAsync(user.Id, previousHash);

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

    // Confirms the user's current password (used to gate sensitive self-service
    // actions like regenerating the recovery phrase). Returns false for an empty
    // password or unknown user rather than throwing.
    public async Task<bool> VerifyPasswordAsync(string userId, string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<string>? NewWords, string? AccessToken)> RecoverAsync(
        string email, List<string> words, string newPassword)
    {
        const string GenericFailure = "Invalid email or recovery phrase";

        if (string.IsNullOrWhiteSpace(email) || words == null || string.IsNullOrWhiteSpace(newPassword))
            return (false, GenericFailure, null, null);

        User? user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            _recoveryService.PerformDummyVerify();  // equalize timing to mask account enumeration
            return (false, GenericFailure, null, null);
        }

        bool valid = await _recoveryService.VerifyRecoveryPhraseAsync(user.Id, words);
        if (!valid)
            return (false, GenericFailure, null, null);

        // Block reuse of the current password or either of the two most recent
        // previous ones — before touching anything else, so a rejected reuse
        // leaves the account fully intact.
        if (await _passwordHistory.IsReusedAsync(user, newPassword))
            return (false, ReuseRejection, null, null);

        // Capture the current hash so we can append it to history once the
        // reset succeeds (becomes the "previous" entry for the next change).
        string? previousHash = user.PasswordHash;

        // Reset the password atomically via Identity's reset-token flow.
        // ResetPasswordAsync validates the new password BEFORE replacing the
        // existing hash — a weak/rejected new password leaves the old one intact
        // rather than bricking the account.
        string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!resetResult.Succeeded)
        {
            // Password policy is public information — surface the specific rule that
            // failed so the legitimate user can fix it. This is distinct from the
            // generic "email or phrase" failure, which intentionally hides which
            // half mismatched to block account enumeration.
            string detail = string.Join(" ", resetResult.Errors.Select(e => e.Description));
            return (false, string.IsNullOrWhiteSpace(detail) ? "Invalid new password" : detail, null, null);
        }

        if (!string.IsNullOrEmpty(previousHash))
            await _passwordHistory.RecordAsync(user.Id, previousHash);

        user.PasswordChangedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Rotate the recovery phrase FIRST so the just-used phrase is dead even
        // if the session-revocation step crashes. This preserves the one-shot
        // invariant under partial failure.
        RecoveryPhraseDTO? newPhrase = await _recoveryService.GenerateRecoveryPhraseAsync(user.Id);

        // Kick every live session and revoke every active refresh token.
        await _sessionService.RevokeAllSessionsAsync(user.Id);

        var activeRefreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync();
        foreach (var rt in activeRefreshTokens)
            rt.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Issue a new access token so the client can immediately PUT the
        // re-wrapped key bundle (paired with the new recovery phrase) to
        // /api/User/me/encryption-keys without forcing a separate login.
        // Same session caveat as Register: create a Session + RefreshToken so
        // the per-request session validator accepts the fresh JWT.
        IList<string> roles = await _userManager.GetRolesAsync(user);
        JwtTokenService.TokenPair tokenPair = _tokens.CreateTokenPair(user, roles);

        RefreshToken newRefreshToken = new()
        {
            Id = Guid.NewGuid(),
            Token = HashRefreshToken(tokenPair.RefreshToken),
            UserId = user.Id,
            ExpiresAt = tokenPair.RefreshTokenExpiresUtc,
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(newRefreshToken);
        await _sessionService.CreateSessionAsync(
            user.Id,
            tokenPair.AccessToken,
            deviceInfo: "Recovery",
            deviceKind: "web",
            ipAddress: null,
            refreshTokenId: newRefreshToken.Id);
        await _context.SaveChangesAsync();

        return (true, "Account recovered. Save your new recovery phrase.", newPhrase?.Words, tokenPair.AccessToken);
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
