using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services
{
    public interface IAuthService
    {
        Task<IdentityResult> RegisterAsync(RegisterDTO model);
        Task<LoginResult> LoginAsync(LoginDTO model);
        Task<LoginResult> RefreshAsync(string refreshToken);

        Task LogoutAsync();

        Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model);
        Task<IdentityResult> ResetPasswordAsync(ResetPasswordDTO model);
        Task<NotImplementedException> ResendConfirmationEmailAsync(ResendConfirmationEmailDTO model);
    }


}