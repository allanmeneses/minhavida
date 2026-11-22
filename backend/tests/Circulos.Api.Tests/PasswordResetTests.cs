using System.Net;
using System.Net.Http.Json;
using Circulos.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class PasswordResetTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasswordResetTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EsqueciSenha_SempreNoContentENaoVazaSeOEmailExiste()
    {
        var client = _factory.CreateClient();
        var missing = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "naoexiste@exemplo.com" });
        Assert.Equal(HttpStatusCode.NoContent, missing.StatusCode);

        var auth = await AuthHelper.RegisterAsync(client);
        var existing = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = auth.Email });
        Assert.Equal(HttpStatusCode.NoContent, existing.StatusCode);
    }

    [Fact]
    public async Task TokenDoEmail_RedefineASenhaUmaVez()
    {
        var client = _factory.CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@exemplo.com";
        await AuthHelper.RegisterAsync(client, email, "senha-antiga-1");
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).EnsureSuccessStatusCode();

        var token = await LastEmailToken(email, "Use este codigo para redefinir a senha: ");
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "senha-nova-99" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-antiga-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-nova-99" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "outra-senha-99" });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    private async Task<string> LastEmailToken(string to, string prefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.SentEmails.OrderByDescending(x => x.SentAt).FirstAsync(x => x.To == to);
        var index = message.Body.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(index >= 0);
        return message.Body[(index + prefix.Length)..].Trim();
    }
}
