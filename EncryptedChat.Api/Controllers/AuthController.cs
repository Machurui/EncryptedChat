using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IAuthService authService) : ControllerBase
{
    private readonly IAuthService _auth = authService;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDTO model)
    {
        IdentityResult result = await _auth.RegisterAsync(model);

        if (result.Succeeded)
            return Ok(new { Message = "User created successfully" });

        List<string> errors = result.Errors.Select(e => e.Description).ToList();
        return BadRequest(new { Message = errors });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO model)
    {
        LoginResult result = await _auth.LoginAsync(model);

        if (!result.Succeeded)
            return BadRequest(new { Message = "Invalid login attempt" });

        SetAccessTokenCookie(result.AccessToken!, result.ExpiresUtc!.Value);
        SetRefreshTokenCookie(result.RefreshToken!);

        return Ok(new
        {
            expiresUtc = result.ExpiresUtc,
            message = "Login successful"
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        string? refreshToken = Request.Cookies["ec.refreshToken"];

        await _auth.LogoutAsync(refreshToken);

        ClearCookie("ec.accessToken");
        ClearCookie("ec.refreshToken");

        return Ok(new { Message = "Logged out" });
    }

    public record RefreshRequest(string? RefreshToken);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? req)
    {
        string? refreshToken = req?.RefreshToken ?? Request.Cookies["ec.refreshToken"];

        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(new { Message = "No refresh token" });

        LoginResult result = await _auth.RefreshAsync(refreshToken);

        if (!result.Succeeded)
            return Unauthorized(new { Message = "Invalid refresh token" });

        SetAccessTokenCookie(result.AccessToken!, result.ExpiresUtc!.Value);
        SetRefreshTokenCookie(result.RefreshToken!);

        return Ok(new
        {
            expiresUtc = result.ExpiresUtc,
            message = "Token refreshed"
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO model)
    {
        throw new NotImplementedException();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDTO model)
    {
        throw new NotImplementedException();
    }

    [HttpPost("resend-confirmation-email")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult ResendConfirmationEmail(ResendConfirmationEmailDTO model)
    {
        throw new NotImplementedException();
    }

    [HttpGet("signalr-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult GetSignalRToken()
    {
        string? accessToken = Request.Cookies["ec.accessToken"];
        if (string.IsNullOrEmpty(accessToken))
            return Unauthorized(new { Message = "No access token" });

        return Ok(new { Token = accessToken });
    }

    private void SetAccessTokenCookie(string token, DateTime expiresUtc)
    {
        CookieOptions options = new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expiresUtc,
            Path = "/"
        };
        Response.Cookies.Append("ec.accessToken", token, options);
    }

    private void SetRefreshTokenCookie(string token)
    {
        CookieOptions options = new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(7),
            Path = "/"
        };
        Response.Cookies.Append("ec.refreshToken", token, options);
    }

    private void ClearCookie(string name)
    {
        CookieOptions options = new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/"
        };
        Response.Cookies.Append(name, "", options);
    }
}
