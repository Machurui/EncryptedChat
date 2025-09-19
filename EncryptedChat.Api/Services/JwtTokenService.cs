using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public class JwtTokenService
{
    private readonly IConfiguration _cfg;

    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    public record TokenPair(string AccessToken, DateTime ExpiresUtc, string? RefreshToken = null);

    public TokenPair CreateAccessToken(User user, IEnumerable<string>? roles = null,
        TimeSpan? lifetime = null, string? refreshToken = null)
    {
        var jwt = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new("name", user.Name ?? "") // optional, add your custom field
        };

        if (roles != null)
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenPair(accessToken, expires, refreshToken);
    }
}
