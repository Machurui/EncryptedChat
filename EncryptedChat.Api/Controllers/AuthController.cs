using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace EncryptedChat.Controllers;

[Route("api/[controller]")]
[ApiController]
public partial class AuthController(IAuthService authService, IConfiguration configuration) : ControllerBase
{
    private readonly IAuthService _auth = authService;
    private readonly IConfiguration _config = configuration;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDTO model)
    {
        (IdentityResult, IReadOnlyList<string>?, string?) registerRequest = await _auth.RegisterAsync(model);

        if (registerRequest.Item1.Succeeded)
        {
            // Set the http-only access cookie too, so subsequent calls from
            // the same browser are auto-authed (matches the Login flow).
            if (!string.IsNullOrEmpty(registerRequest.Item3))
                SetAccessTokenCookie(registerRequest.Item3, DateTime.UtcNow.AddMinutes(15));

            return Ok(new RegisterResultDTO(
                "User created successfully",
                registerRequest.Item2 ?? [],
                registerRequest.Item3 ?? string.Empty));
        }

        List<string> errors = [.. registerRequest.Item1.Errors.Select(e => e.Description)];
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
            Match match = MyRegex().Match(userAgent);
            browser = match.Success ? $"Chrome {match.Groups[1].Value}" : "Chrome";
        }
        else if (userAgent.Contains("Firefox"))
        {
            Match match = MyRegex1().Match(userAgent);
            browser = match.Success ? $"Firefox {match.Groups[1].Value}" : "Firefox";
        }
        else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome"))
        {
            Match match = MyRegex2().Match(userAgent);
            browser = match.Success ? $"Safari {match.Groups[1].Value}" : "Safari";
        }
        else if (userAgent.Contains("Edg"))
        {
            Match match = MyRegex3().Match(userAgent);
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
        (bool, string, IReadOnlyList<string>?, string?) recoverRequest = await _auth.RecoverAsync(dto.Email, dto.Words, dto.NewPassword);

        if (!recoverRequest.Item1)
            return BadRequest(new { Message = recoverRequest.Item2 });

        // Set the http-only access cookie so subsequent calls from the same
        // browser are auto-authed for the inline key-rewrap step.
        if (!string.IsNullOrEmpty(recoverRequest.Item4))
            SetAccessTokenCookie(recoverRequest.Item4, DateTime.UtcNow.AddMinutes(15));

        return Ok(new RecoverResultDTO(recoverRequest.Item2, recoverRequest.Item3 ?? [], recoverRequest.Item4 ?? string.Empty));
    }

    // Secure/SameSite are configurable so the same code works cross-origin in dev
    // (defaults: Secure=true, SameSite=None) and same-origin behind a reverse proxy
    // over HTTP in Docker (Cookies:Secure=false, Cookies:SameSite=Lax).
    private CookieOptions BuildCookieOptions(DateTime expiresUtc)
    {
        bool secure = _config.GetValue("Cookies:Secure", true);
        SameSiteMode sameSite = Enum.TryParse(_config["Cookies:SameSite"], ignoreCase: true, out SameSiteMode parsed)
            ? parsed
            : SameSiteMode.None;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Expires = expiresUtc,
            Path = "/"
        };
    }

    private void SetAccessTokenCookie(string token, DateTime expiresUtc)
    {
        Response.Cookies.Append("ec.accessToken", token, BuildCookieOptions(expiresUtc));
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append("ec.refreshToken", token, BuildCookieOptions(DateTime.UtcNow.AddDays(7)));
    }

    private void ClearCookie(string name)
    {
        // Use the same config-driven Secure/SameSite as the set helpers, with a past
        // expiry — otherwise over HTTP (Docker) a Secure clear-cookie is ignored and
        // the cookie survives logout.
        Response.Cookies.Append(name, "", BuildCookieOptions(DateTime.UtcNow.AddDays(-1)));
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"Chrome/(\d+)")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
    [GeneratedRegex(@"Firefox/(\d+)")]
    private static partial Regex MyRegex1();
    [GeneratedRegex(@"Version/(\d+)")]
    private static partial Regex MyRegex2();
    [GeneratedRegex(@"Edg/(\d+)")]
    private static partial Regex MyRegex3();
}
