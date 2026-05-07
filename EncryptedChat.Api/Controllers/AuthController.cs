using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EncryptedChat.Controllers
{
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

        // Returns a JWT access token in HTTP-only cookie
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO model)
        {
            LoginResult result = await _auth.LoginAsync(model);

            if (!result.Succeeded)
                return BadRequest(new { Message = "Invalid login attempt" });

            CookieOptions cookieOptions = new()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = result.ExpiresUtc,
                Path = "/"
            };
            Response.Cookies.Append("ec.accessToken", result.AccessToken!, cookieOptions);

            return Ok(new
            {
                expiresUtc = result.ExpiresUtc,
                message = "Login successful"
            });
        }

        // Clear the HTTP-only cookie
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            CookieOptions cookieOptions = new()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                Path = "/"
            };
            Response.Cookies.Append("ec.accessToken", "", cookieOptions);

            return Ok(new { Message = "Logged out" });
        }

        // Returns current user info if authenticated
        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            string? userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            string? name = User.FindFirst("name")?.Value ?? User.Identity?.Name;
            List<string> roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

            return Ok(new
            {
                userId,
                name,
                roles,
                isAuthenticated = true
            });
        }

        // Returns the access token for SignalR connections (protected by cookie auth)
        [HttpGet("signalr-token")]
        [Authorize]
        public IActionResult GetSignalRToken()
        {
            if (Request.Cookies.TryGetValue("ec.accessToken", out string? token))
            {
                return Ok(new { token });
            }
            return Unauthorized(new { Message = "No valid session" });
        }

        // Optional: refresh flow (left as placeholder)
        public record RefreshRequest(string refreshToken);

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            LoginResult result = await _auth.RefreshAsync(req.refreshToken);
            if (!result.Succeeded)
                return Unauthorized();

            CookieOptions cookieOptions = new()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = result.ExpiresUtc,
                Path = "/"
            };
            Response.Cookies.Append("ec.accessToken", result.AccessToken!, cookieOptions);

            return Ok(new
            {
                expiresUtc = result.ExpiresUtc,
                message = "Token refreshed"
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO model)
        {
            IdentityResult result = await _auth.ForgotPasswordAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Password reset link sent" });

            return BadRequest(result.Errors);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDTO model)
        {
            IdentityResult result = await _auth.ResetPasswordAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Password reset successful" });

            return BadRequest(result.Errors);
        }

        [HttpPost("resend-confirmation-email")]
        [Authorize]
        public IActionResult ResendConfirmationEmail(ResendConfirmationEmailDTO model)
        {
            throw new NotImplementedException();
        }
    }
}
