using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services;

public class AuthService
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
        {
            await _userManager.AddToRoleAsync(user, "User");
        }
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
        {
            return Microsoft.AspNetCore.Identity.SignInResult.Failed;
        }

        await _signInManager.RefreshSignInAsync(user);

        return Microsoft.AspNetCore.Identity.SignInResult.Success;
    }
}