using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;

namespace EncryptedChat.Services;

public interface IAuthService
{
    Task<IdentityResult> RegisterAsync(RegisterDTO model);
    Task<LoginResult> LoginAsync(LoginDTO model);
    Task<LoginResult> RefreshAsync(string refreshToken);
    Task LogoutAsync(string? refreshToken);
    Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model);
    Task<IdentityResult> ResetPasswordAsync(ResetPasswordDTO model);
    Task<IdentityResult> ResendConfirmationEmailAsync(ResendConfirmationEmailDTO model);
}