using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EncryptedChat.Models;

namespace EncryptedChat.Services;

public class JwtTokenService(IConfiguration cfg)
{
    private readonly IConfiguration _cfg = cfg;

    public record TokenPair(string AccessToken, DateTime AccessTokenExpiresUtc, string RefreshToken, DateTime RefreshTokenExpiresUtc);

    public TokenPair CreateTokenPair(User user, IEnumerable<string>? roles = null)
    {
        IConfigurationSection jwt = _cfg.GetSection("Jwt");
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(jwt["Key"]!));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new("name", user.Name ?? "")
        ];

        if (roles != null)
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        DateTime accessTokenExpires = DateTime.UtcNow.AddMinutes(15);

        JwtSecurityToken token = new(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: accessTokenExpires,
            signingCredentials: creds
        );

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        string refreshToken = GenerateRefreshToken();
        DateTime refreshTokenExpires = DateTime.UtcNow.AddDays(7);

        return new TokenPair(accessToken, accessTokenExpires, refreshToken, refreshTokenExpires);
    }

    private static string GenerateRefreshToken()
    {
        byte[] randomBytes = new byte[64];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
