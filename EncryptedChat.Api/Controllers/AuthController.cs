using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
        var (result, recoveryWords, accessToken) = await _auth.RegisterAsync(model);

        if (result.Succeeded)
        {
            // Set the http-only access cookie too, so subsequent calls from
            // the same browser are auto-authed (matches the Login flow).
            if (!string.IsNullOrEmpty(accessToken))
                SetAccessTokenCookie(accessToken, DateTime.UtcNow.AddMinutes(15));

            return Ok(new RegisterResultDTO(
                "User created successfully",
                recoveryWords ?? Array.Empty<string>(),
                accessToken ?? string.Empty));
        }

        List<string> errors = result.Errors.Select(e => e.Description).ToList();
        return BadRequest(new { Message = errors });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO model)
    {
        string userAgent = Request.Headers.UserAgent.ToString();
        string deviceInfo = ParseDeviceInfo(userAgent);
        string deviceKind = DetectDeviceKind(userAgent);
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        LoginResult result = await _auth.LoginAsync(model, deviceInfo, deviceKind, ipAddress);

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

    private static string ParseDeviceInfo(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "Unknown device";

        string browser = "Unknown";
        string os = "Unknown";

        if (userAgent.Contains("Chrome") && !userAgent.Contains("Edg"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(userAgent, @"Chrome/(\d+)");
            browser = match.Success ? $"Chrome {match.Groups[1].Value}" : "Chrome";
        }
        else if (userAgent.Contains("Firefox"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(userAgent, @"Firefox/(\d+)");
            browser = match.Success ? $"Firefox {match.Groups[1].Value}" : "Firefox";
        }
        else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(userAgent, @"Version/(\d+)");
            browser = match.Success ? $"Safari {match.Groups[1].Value}" : "Safari";
        }
        else if (userAgent.Contains("Edg"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(userAgent, @"Edg/(\d+)");
            browser = match.Success ? $"Edge {match.Groups[1].Value}" : "Edge";
        }

        if (userAgent.Contains("Windows"))
            os = "Windows";
        else if (userAgent.Contains("Mac OS X") || userAgent.Contains("Macintosh"))
            os = "macOS";
        else if (userAgent.Contains("Linux") && !userAgent.Contains("Android"))
            os = "Linux";
        else if (userAgent.Contains("Android"))
            os = "Android";
        else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            os = "iOS";

        return $"{os} · {browser}";
    }

    private static string DetectDeviceKind(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "web";

        if (userAgent.Contains("Mobile") || userAgent.Contains("Android") ||
            userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            return "mobile";

        return "web";
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

        string? oldAccessToken = Request.Cookies["ec.accessToken"];
        string userAgent = Request.Headers.UserAgent.ToString();
        string deviceInfo = ParseDeviceInfo(userAgent);
        string deviceKind = DetectDeviceKind(userAgent);
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        LoginResult result = await _auth.RefreshAsync(refreshToken, oldAccessToken, deviceInfo, deviceKind, ipAddress);

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

    [HttpPost("recover")]
    [EnableRateLimiting("AccountRecover")]
    public async Task<IActionResult> Recover([FromBody] RecoverRequestDTO dto)
    {
        var (success, message, newWords, accessToken) = await _auth.RecoverAsync(dto.Email, dto.Words, dto.NewPassword);

        if (!success)
            return BadRequest(new { Message = message });

        // Set the http-only access cookie so subsequent calls from the same
        // browser are auto-authed for the inline key-rewrap step.
        if (!string.IsNullOrEmpty(accessToken))
            SetAccessTokenCookie(accessToken, DateTime.UtcNow.AddMinutes(15));

        return Ok(new RecoverResultDTO(message, newWords ?? Array.Empty<string>(), accessToken ?? string.Empty));
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
