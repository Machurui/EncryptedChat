using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;

namespace EncryptedChat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthDebugController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthDebugController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("validate")]
    [AllowAnonymous]
    public IActionResult ValidateToken()
    {
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            return BadRequest(new { error = "Missing or invalid Authorization header" });

        var token = authHeader.Substring("Bearer ".Length).Trim();

        var jwtSection = _config.GetSection("Jwt");
        var key = jwtSection["Key"];
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            return StatusCode(500, new { error = "JWT configuration is incomplete" });
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var signingKey = new SymmetricSecurityKey(keyBytes);

        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = signingKey,
                    RoleClaimType = ClaimTypes.Role
                },
                out var validatedToken);

            // If we reach here, validation passed
            var claims = principal.Claims
                .Select(c => new { c.Type, c.Value })
                .ToList();
            Console.WriteLine("Token is valid");
            return Ok(new
            {
                message = "Token is valid",
                claims
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Token is invalid");
            return Unauthorized(new
            {
                message = "Token validation failed",
                exception = ex.GetType().Name,
                error = ex.Message
            });
        }
    }
}