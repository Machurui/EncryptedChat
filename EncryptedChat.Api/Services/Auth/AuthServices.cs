using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace EncryptedChat.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager; // still used for password checks/lockout
    private readonly JwtTokenService _tokens;

    public AuthService(UserManager<User> userManager,
                       SignInManager<User> signInManager,
                       JwtTokenService tokens)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokens = tokens;
    }

    public async Task<IdentityResult> RegisterAsync(RegisterDTO model)
    {
        // check email
        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateEmail",
                Description = "Email already in use"
            });
        }

        // check name
        var existingName = await _userManager.Users.AnyAsync(u => u.Name == model.Name);
        if (existingName)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateName",
                Description = "Name already in use"
            });
        }

        var user = new User
        {
            UserName = model.Email,
            Name = model.Name,
            Email = model.Email,
            Level = 1,
            Secret = Guid.NewGuid().ToString("N")
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
            await _userManager.AddToRoleAsync(user, "User");

        return result;
    }

    // NEW SHAPE: returns a result containing the JWT on success
    public async Task<LoginResult> LoginAsync(LoginDTO model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email)
                   ?? await _userManager.FindByNameAsync(model.Email);

        if (user is null)
            return LoginResult.Fail("User not found");

        var pwd = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!pwd.Succeeded)
            return LoginResult.Fail("Invalid password");

        var roles = await _userManager.GetRolesAsync(user);

        // Issue a 15-minute access token
        var token = _tokens.CreateAccessToken(user, roles, TimeSpan.FromMinutes(15));

        return LoginResult.Success(token.AccessToken, token.ExpiresUtc, token.RefreshToken);
    }

    // JWT "logout" is a no-op server-side (client discards token)
    // ✅ new
    public Task LogoutAsync()
    {
        // For JWT there’s nothing to do server-side.
        // Client should just discard its token.
        return Task.CompletedTask;
    }


    // Refresh using a server-stored refresh token (placeholder)
    public async Task<LoginResult> RefreshAsync(string refreshToken)
    {
        // TODO: validate refresh token from your store and load user.
        // For now return failure so you don't accidentally rely on it.
        await Task.CompletedTask;
        return LoginResult.Fail("Refresh not implemented");
    }

    public async Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        var callbackUrl = $"https://yourapp.com/reset-password?token={token}&email={model.Email}";
        var message = $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>";

        // TODO: send email
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ResetPasswordAsync(ResetPasswordDTO model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User not found" });
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        return result;
    }

    public async Task<NotImplementedException> ResendConfirmationEmailAsync(ResendConfirmationEmailDTO model)
    {
        await Task.CompletedTask;
        return new NotImplementedException();
    }
}

// Helper result for login/refresh
public record LoginResult(bool Succeeded, string? AccessToken, DateTime? ExpiresUtc, string? RefreshToken, string? Error)
{
    public static LoginResult Success(string token, DateTime expiresUtc, string? refresh = null)
        => new(true, token, expiresUtc, refresh, null);

    public static LoginResult Fail(string error)
        => new(false, null, null, null, error);
}
