using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Auth;

namespace Circulos.Api.Tests;

public sealed class TwoFactorTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public TwoFactorTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AtivarComQr_ExigeCodigoNoLogin_RecuperacaoEntraUmaVez()
    {
        var client = _factory.CreateClient();
        var email = $"totp-{Guid.NewGuid():N}@exemplo.com";
        var auth = await AuthHelper.RegisterAsync(client, email);
        client.WithToken(auth.Token);

        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var payload = await setup.Content.ReadFromJsonAsync<SetupDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(payload?.Secret));
        Assert.StartsWith("otpauth://totp/Círculos:", payload!.OtpauthUri);
        Assert.False(string.IsNullOrWhiteSpace(payload.QrPngBase64));
        Assert.Equal(8, payload.RecoveryCodes.Count);
        var png = Convert.FromBase64String(payload.QrPngBase64);
        Assert.True(png.Length > 40);
        Assert.Equal(0x89, png[0]);

        var wrong = await client.PostAsJsonAsync("/api/auth/2fa/enable", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var code = Totp.Code(payload.Secret, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/2fa/enable", new { code })).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var challenge = await login.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.True(challenge!.RequiresTwoFactor);
        Assert.Null(challenge.Token);
        Assert.False(string.IsNullOrWhiteSpace(challenge.TwoFactorToken));

        var totpLogin = Totp.Code(payload.Secret, DateTimeOffset.UtcNow);
        var verified = await client.PostAsJsonAsync("/api/auth/2fa/verify", new
        {
            twoFactorToken = challenge.TwoFactorToken,
            code = totpLogin,
        });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var session = await verified.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(session?.Token));

        var loginAgain = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-1" });
        var challenge2 = await loginAgain.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        var recovered = await client.PostAsJsonAsync("/api/auth/2fa/verify", new
        {
            twoFactorToken = challenge2!.TwoFactorToken,
            recoveryCode = payload.RecoveryCodes[0],
        });
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);

        var loginThird = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-1" });
        var challenge3 = await loginThird.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        var reuse = await client.PostAsJsonAsync("/api/auth/2fa/verify", new
        {
            twoFactorToken = challenge3!.TwoFactorToken,
            recoveryCode = payload.RecoveryCodes[0],
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task EnableSobrepostoComLogin_NaoEntregaSessaoSemSegundoFator()
    {
        var session = _factory.CreateClient();
        var loginClient = _factory.CreateClient();
        var email = $"race-2fa-{Guid.NewGuid():N}@exemplo.com";
        var auth = await AuthHelper.RegisterAsync(session, email);
        session.WithToken(auth.Token);
        var setup = await (await session.PostAsync("/api/auth/2fa/setup", null)).Content.ReadFromJsonAsync<SetupDto>(Json);
        var code = Totp.Code(setup!.Secret, DateTimeOffset.UtcNow);

        var enableTask = session.PostAsJsonAsync("/api/auth/2fa/enable", new { code });
        var loginTask = loginClient.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-1" });
        await Task.WhenAll(enableTask, loginTask);
        Assert.Equal(HttpStatusCode.NoContent, (await enableTask).StatusCode);

        var login = await (await loginTask).Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        var status = await session.GetFromJsonAsync<JsonElement>("/api/auth/2fa", Json);
        Assert.True(status.GetProperty("enabled").GetBoolean());
        if (login!.Token is not null)
        {
            var again = await loginClient.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-1" });
            var next = await again.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
            Assert.True(next!.RequiresTwoFactor);
            Assert.Null(next.Token);
        }
        else
        {
            Assert.True(login.RequiresTwoFactor);
        }
    }

    [Fact]
    public async Task Desativar_PedeSenhaECodigo()
    {
        var client = _factory.CreateClient();
        var email = $"off-2fa-{Guid.NewGuid():N}@exemplo.com";
        var auth = await AuthHelper.RegisterAsync(client, email);
        client.WithToken(auth.Token);
        var setup = await (await client.PostAsync("/api/auth/2fa/setup", null)).Content.ReadFromJsonAsync<SetupDto>(Json);
        var enableCode = Totp.Code(setup!.Secret, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/2fa/enable", new { code = enableCode })).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/2fa/disable", new { password = "errada-99", code = enableCode })).StatusCode);

        var disableCode = Totp.Code(setup.Secret, DateTimeOffset.UtcNow);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/2fa/disable", new { password = "senha-segura-1", code = disableCode })).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-1" });
        var payload = await login.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(payload!.RequiresTwoFactor);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
    }

    private sealed record SetupDto(string Secret, string OtpauthUri, string QrPngBase64, IReadOnlyList<string> RecoveryCodes);
}
