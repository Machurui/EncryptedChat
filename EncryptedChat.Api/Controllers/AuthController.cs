using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService authService)
        {
            _auth = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO model)
        {
            var result = await _auth.RegisterAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "User created successfully" });

            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { Message = errors });
        }

        // Returns a JWT access token
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO model)
        {
            var result = await _auth.LoginAsync(model);

            if (!result.Succeeded)
                return BadRequest(new { Message = "Invalid login attempt" });

            // return token payload
            return Ok(new
            {
                accessToken = result.AccessToken!,
                expiresUtc = result.ExpiresUtc,
                refreshToken = result.RefreshToken // null unless you wire refresh
            });
        }

        // With JWT there is nothing to "server-logout" (client deletes token).
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { Message = "Logged out (client should discard token)." });
        }

        // Optional: refresh flow (left as placeholder)
        public record RefreshRequest(string refreshToken);

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            var result = await _auth.RefreshAsync(req.refreshToken);
            if (!result.Succeeded)
                return Unauthorized();

            return Ok(new
            {
                accessToken = result.AccessToken!,
                expiresUtc = result.ExpiresUtc
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO model)
        {
            var result = await _auth.ForgotPasswordAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Password reset link sent" });

            return BadRequest(result.Errors);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDTO model)
        {
            var result = await _auth.ResetPasswordAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Password reset successful" });

            return BadRequest(result.Errors);
        }

        [HttpPost("resend-confirmation-email")]
        [Authorize]
        public async Task<NotImplementedException> ResendConfirmationEmail(ResendConfirmationEmailDTO model)
        {
            return new NotImplementedException();
        }
    }
}
