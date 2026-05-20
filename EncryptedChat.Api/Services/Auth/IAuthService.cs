using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;

namespace EncryptedChat.Services;

public interface IAuthService
{
    Task<IdentityResult> RegisterAsync(RegisterDTO model);
    Task<LoginResult> LoginAsync(LoginDTO model, string? deviceInfo = null, string? deviceKind = null, string? ipAddress = null);
    Task<LoginResult> RefreshAsync(string refreshToken, string? oldAccessToken = null, string? deviceInfo = null, string? deviceKind = null, string? ipAddress = null);
    Task LogoutAsync(string? refreshToken);
    Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model);
    Task<IdentityResult> ResetPasswordAsync(ResetPasswordDTO model);
    Task<IdentityResult> ResendConfirmationEmailAsync(ResendConfirmationEmailDTO model);
    Task<IdentityResult> ChangePasswordAsync(string userId, ChangePasswordDTO model);
    Task<DateTime?> GetPasswordChangedAtAsync(string userId);
}