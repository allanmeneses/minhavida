using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Circulos.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Circulos.Api.Auth;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string Create(User user)
    {
        var section = configuration.GetSection("Jwt");
        var keyValue = section["Key"] ?? throw new InvalidOperationException("Jwt:Key nao configurada.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var hours = int.TryParse(section["ExpiresHours"], out var parsed) ? parsed : 12;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("name", user.Profile.DisplayName),
        };

        var token = new JwtSecurityToken(
            issuer: section["Issuer"],
            audience: section["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
