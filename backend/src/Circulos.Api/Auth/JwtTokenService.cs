using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Circulos.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Circulos.Api.Auth;

public sealed class JwtTokenService(IConfiguration configuration, TimeProvider clock)
{
    public const int DefaultAccessMinutes = 15;
    public const int DefaultRefreshDays = 7;

    public string CreateAccess(User user)
    {
        var section = configuration.GetSection("Jwt");
        var keyValue = section["Key"] ?? throw new InvalidOperationException("Jwt:Key nao configurada.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
        var minutes = AccessMinutes();
        var now = clock.GetUtcNow().UtcDateTime;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("name", user.Profile.DisplayName),
            new Claim("typ", "access"),
        };

        var token = new JwtSecurityToken(
            issuer: section["Issuer"],
            audience: section["Audience"],
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int AccessMinutes()
    {
        var section = configuration.GetSection("Jwt");
        if (int.TryParse(section["AccessTokenMinutes"], out var minutes) && minutes > 0)
        {
            return minutes;
        }

        if (int.TryParse(section["ExpiresHours"], out var hours) && hours > 0)
        {
            return hours * 60;
        }

        return DefaultAccessMinutes;
    }

    public int RefreshDays()
    {
        var section = configuration.GetSection("Jwt");
        return int.TryParse(section["RefreshTokenDays"], out var days) && days > 0
            ? days
            : DefaultRefreshDays;
    }
}
