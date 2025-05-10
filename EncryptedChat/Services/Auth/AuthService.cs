using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public AuthService(UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<IdentityResult> RegisterAsync(RegisterDTO model)
    {
        var user = new User
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Level = 1,
            Secret = Guid.NewGuid().ToString("N")
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
            await _userManager.AddToRoleAsync(user, "User");

        return result;
    }

    public async Task<Microsoft.AspNetCore.Identity.SignInResult> LoginAsync(LoginDTO model)
    {
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            isPersistent: false,
            lockoutOnFailure: false
        );

        return result;
    }

    public async Task<SignOutResult> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        
        return new SignOutResult();
    }

    public async Task<Microsoft.AspNetCore.Identity.SignInResult> RefreshAsync(ClaimsPrincipal userPrincipal)
    {
        var user = await _userManager.GetUserAsync(userPrincipal);

        if (user == null)
            return Microsoft.AspNetCore.Identity.SignInResult.Failed;

        await _signInManager.RefreshSignInAsync(user);

        return Microsoft.AspNetCore.Identity.SignInResult.Success;
    }

    public async Task<IdentityResult> ForgotPasswordAsync(ForgotPasswordDTO model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        var callbackUrl = $"https://yourapp.com/reset-password?token={token}&email={model.Email}";
        var message = $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>";

        // Send email logic here
        // await _emailSender.SendEmailAsync(model.Email, "Reset Password", message);
        // For testing purposes, we can just return success
        // In a real application, you would send the email here

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
        return new NotImplementedException();
    }
}