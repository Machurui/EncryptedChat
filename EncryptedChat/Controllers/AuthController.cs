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
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO model)
        {
            var result = await _authService.RegisterAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "User created successfully" });

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO model)
        {
            var result = await _authService.LoginAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Login successful" });

            return BadRequest(new { Message = "Invalid login attempt" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return Ok(new { Message = "Logout successful" });
        }


        [HttpPost("refresh")]
        [Authorize]
        public async Task<IActionResult> Refresh()
        {
            var result = await _authService.RefreshAsync(User);

            if (result.Succeeded)
                return Ok(new { message = "Session refreshed" });
            else
                return Unauthorized();
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDTO model)
        {
            var result = await _authService.ForgotPasswordAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Password reset link sent" });

            return BadRequest(result.Errors);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDTO model)
        {
            var result = await _authService.ResetPasswordAsync(model);

            if (result.Succeeded)
                return Ok(new { Message = "Password reset successful" });

            return BadRequest(result.Errors);
        }

        [HttpPost("resend-confirmation-email")]
        [Authorize]
        public async Task<NotImplementedException> ResendConfirmationEmail(ResendConfirmationEmailDTO model)
        {
            // var result = await _authService.ResendConfirmationEmailAsync(model);

            // if (result.Succeeded)
            //     return Ok(new { Message = "Confirmation email resent" });

            // return BadRequest(result.Errors);

            return new NotImplementedException();
        }
    }
}