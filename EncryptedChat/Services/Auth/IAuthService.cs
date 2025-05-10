using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services
{
    public interface IAuthService
    {
        Task<IdentityResult> RegisterAsync(RegisterDTO model);
        Task<Microsoft.AspNetCore.Identity.SignInResult> LoginAsync(LoginDTO model);

        Task<SignOutResult> LogoutAsync();

        Task<Microsoft.AspNetCore.Identity.SignInResult> RefreshAsync(ClaimsPrincipal userPrincipal);

        Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model);

        Task<IdentityResult> ResetPasswordAsync(ResetPasswordDTO model);

        Task<NotImplementedException> ResendConfirmationEmailAsync(ResendConfirmationEmailDTO model);
    }
}