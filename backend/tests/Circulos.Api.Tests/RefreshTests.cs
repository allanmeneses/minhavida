using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Circulos.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Circulos.Api.Tests;

public sealed class RefreshTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public RefreshTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_DevolveAccessERefresh_AccessExpiraERefreshRenova()
    {
        var client = _factory.CreateClient();
        var email = $"refresh-{Guid.NewGuid():N}@exemplo.com";
        var auth = await AuthHelper.RegisterAsync(client, email);
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        var access = ReadJwt(auth.Token);
        Assert.True(access.ValidTo - access.ValidFrom <= TimeSpan.FromMinutes(16));
        Assert.True(access.ValidTo - access.ValidFrom >= TimeSpan.FromMinutes(14));

        client.WithToken(ExpiredAccess(auth.UserId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var payload = await rotated.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload!.RefreshToken));
        Assert.NotEqual(auth.RefreshToken, payload.RefreshToken);

        client.WithToken(payload.Token!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task RefreshSobreposto_SoUmaRotacaoVence()
    {
        var primeiro = _factory.CreateClient();
        var segundo = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(primeiro);
        var body = new { refreshToken = auth.RefreshToken };

        var tarefa1 = primeiro.PostAsJsonAsync("/api/auth/refresh", body);
        var tarefa2 = segundo.PostAsJsonAsync("/api/auth/refresh", body);
        var respostas = await Task.WhenAll(tarefa1, tarefa2);
        var codes = respostas.Select(r => r.StatusCode).ToArray();
        Assert.Contains(HttpStatusCode.OK, codes);
        Assert.Contains(HttpStatusCode.Unauthorized, codes);

        var ok = respostas.Single(r => r.StatusCode == HttpStatusCode.OK);
        var payload = await ok.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        var confirm = await primeiro.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = payload!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    [Fact]
    public async Task Logout_RevogaRefresh_NaoRenovaMais()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task LogoutSobrepostoComRefresh_NaoDeixaDoisRefreshValidos()
    {
        var client = _factory.CreateClient();
        var other = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        var body = new { refreshToken = auth.RefreshToken };

        var refreshTask = other.PostAsJsonAsync("/api/auth/refresh", body);
        var logoutTask = client.PostAsJsonAsync("/api/auth/logout", body);
        await Task.WhenAll(refreshTask, logoutTask);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alive = await db.RefreshTokens.CountAsync(x => x.UserId == auth.UserId && x.RevokedAt == null);
        Assert.True(alive <= 1);
        if (alive == 1)
        {
            var token = (await refreshTask).StatusCode == HttpStatusCode.OK
                ? (await (await refreshTask).Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json))!.RefreshToken
                : null;
            if (token is not null)
            {
                var afterLogout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = token });
                Assert.Equal(HttpStatusCode.NoContent, afterLogout.StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token })).StatusCode);
            }
        }
    }

    [Fact]
    public async Task ResetSenha_RevogaRefresh()
    {
        var client = _factory.CreateClient();
        var email = $"revoga-{Guid.NewGuid():N}@exemplo.com";
        var auth = await AuthHelper.RegisterAsync(client, email, "senha-antiga-1");
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.SentEmails.OrderByDescending(x => x.SentAt).FirstAsync(x => x.To == email);
        var token = message.Body.Split("senha: ")[1].Trim();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "senha-nova-99" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken })).StatusCode);
    }

    private static JwtSecurityToken ReadJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string ExpiredAccess(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("troque-esta-chave-local-de-desenvolvimento-32"));
        var jwt = new JwtSecurityToken(
            issuer: "Circulos",
            audience: "Circulos",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("typ", "access"),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-20),
            expires: DateTime.UtcNow.AddMinutes(-2),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
