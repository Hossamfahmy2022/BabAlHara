using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BabAlHara.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace BabAlHara.Api.Services;

public class JwtTokenService(IConfiguration config)
{
    public (string Token, DateTime ExpiresAt) CreateToken(AdminUser user)
    {
        var issuer = config["Jwt:Issuer"] ?? "BabAlHara.Api";
        var audience = config["Jwt:Audience"] ?? "BabAlHara.Admin";
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var hours = int.TryParse(config["Jwt:ExpiryHours"], out var parsed) && parsed > 0 ? parsed : 12;

        var expiresAt = DateTime.UtcNow.AddHours(hours);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
