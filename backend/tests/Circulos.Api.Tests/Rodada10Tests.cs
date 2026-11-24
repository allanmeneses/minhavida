using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class Rodada10Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada10Tests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Estatisticas_ContaUsoAgregado_SemConteudo_SoAdminLe()
    {
        var pessoa = _factory.CreateClient();
        pessoa.WithToken((await AuthHelper.RegisterWithLegacyAreasAsync(pessoa, _factory)).Token);
        var areas = await pessoa.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        (await pessoa.PostAsJsonAsync($"/api/life-areas/{diario}/entries", new { title = "Segredo da vida" })).EnsureSuccessStatusCode();
        var shortcut = await (await pessoa.PostAsJsonAsync("/api/shortcuts", new { label = "Tomei", actionKind = "done" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await pessoa.PostAsync($"/api/shortcuts/{shortcut.GetProperty("id").GetGuid()}/run", null)).EnsureSuccessStatusCode();
        (await pessoa.PostAsJsonAsync("/api/circles", new { name = "Grupo ativo" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await pessoa.GetAsync("/api/admin/stats")).StatusCode);

        var admin = await AdminClient();
        var stats = await admin.GetFromJsonAsync<JsonElement>("/api/admin/stats", Json);
        Assert.True(stats.GetProperty("shortcutsTriggered").GetInt64() >= 1);
        Assert.True(stats.GetProperty("entriesCreated").GetInt64() >= 1);
        Assert.True(stats.GetProperty("activeCircles").GetInt64() >= 1);
        var json = stats.GetRawText();
        Assert.DoesNotContain("Segredo da vida", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Tomei", json, StringComparison.Ordinal);
        Assert.DoesNotContain("@exemplo.com", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Estatisticas_DoisAtalhosSobrepostos_ContamOsDois()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var created = await (await client.PostAsJsonAsync("/api/shortcuts", new { label = "Dois", actionKind = "done" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var before = await AdminStatsShortcuts();
        var responses = await Task.WhenAll(a.PostAsync($"/api/shortcuts/{id}/run", null), b.PostAsync($"/api/shortcuts/{id}/run", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        Assert.Equal(before + 2, await AdminStatsShortcuts());
    }

    [Fact]
    public async Task CirculoTemporario_DataDeFimPassada_ArquivaSozinhoUmaVez()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var endsAt = DateTime.UtcNow.AddMinutes(-1);
        var created = await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Curso curto",
            kind = "temporary",
            endsAt,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var circle = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = circle.GetProperty("id").GetGuid();
        Assert.Equal("active", circle.GetProperty("status").GetString());

        var lasting = await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Família",
            kind = "lasting",
            endsAt,
        });
        Assert.Equal(HttpStatusCode.BadRequest, lasting.StatusCode);

        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(a.GetAsync("/api/circles"), b.GetAsync("/api/circles"));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{id}", Json);
        Assert.Equal("archived", detail.GetProperty("status").GetString());
        var recap = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{id}/recap", Json);
        Assert.Equal("archived", recap.GetProperty("status").GetString());
        var log = detail.GetProperty("log").EnumerateArray().Count(x => x.GetProperty("action").GetString() == "archived");
        Assert.Equal(1, log);
    }

    [Fact]
    public async Task CirculoDuradouro_DataDeFim_NaoConsomeCota()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var invalid = await client.PostAsJsonAsync("/api/circles", new
        {
            name = "Inválido",
            kind = "lasting",
            endsAt = DateTime.UtcNow,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/circles", new { name = "Um" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/circles", new { name = "Dois" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/circles", new { name = "Tres" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/circles", new { name = "Quatro" })).StatusCode);
    }

    [Fact]
    public async Task RecapEArquivo_GratisLeNoApp_NaoDependeDePlanoPago()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var circle = await (await client.PostAsJsonAsync("/api/circles", new { name = "Evento", kind = "temporary" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await client.PostAsJsonAsync($"/api/circles/{circle.GetProperty("id").GetGuid()}/posts", new { body = "Minha história" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/circles/{circle.GetProperty("id").GetGuid()}/archive", null)).EnsureSuccessStatusCode();
        var recap = await client.GetAsync($"/api/circles/{circle.GetProperty("id").GetGuid()}/recap");
        Assert.Equal(HttpStatusCode.OK, recap.StatusCode);
        using var doc = JsonDocument.Parse(await recap.Content.ReadAsStringAsync());
        Assert.Equal("archived", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains(doc.RootElement.GetProperty("myTitles").EnumerateArray(), x => x.GetString() == "Minha história");
        var share = await client.PostAsync($"/api/circles/{circle.GetProperty("id").GetGuid()}/recap/share", null);
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
    }

    [Fact]
    public async Task DenunciaEBloqueio_AdminBloqueiaEDesbloqueiaComRegistro()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Alvo");
        dono.WithToken(donoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Radar", visibility = "public", discoverable = true }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();
        var post = await (await dono.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "Post alvo" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        var reporter = _factory.CreateClient();
        reporter.WithToken((await AuthHelper.RegisterAsync(reporter, name: "Reporter")).Token);
        (await reporter.PostAsJsonAsync("/api/reports", new { targetKind = "profile", targetId = donoAuth.UserId })).EnsureSuccessStatusCode();
        (await reporter.PostAsJsonAsync("/api/reports", new { targetKind = "circle", targetId = circleId })).EnsureSuccessStatusCode();
        (await reporter.PostAsJsonAsync("/api/reports", new { targetKind = "post", targetId = post.GetProperty("id").GetGuid() }))
            .EnsureSuccessStatusCode();

        var overlapA = _factory.CreateClient();
        overlapA.WithToken((await AuthHelper.RegisterAsync(overlapA)).Token);
        var overlapB = _factory.CreateClient();
        overlapB.WithToken((await AuthHelper.RegisterAsync(overlapB)).Token);
        var overlapped = await Task.WhenAll(
            overlapA.PostAsJsonAsync("/api/reports", new { targetKind = "circle", targetId = circleId }),
            overlapB.PostAsJsonAsync("/api/reports", new { targetKind = "circle", targetId = circleId }));
        Assert.All(overlapped, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        var admin = await AdminClient();
        var reports = await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/reports", Json);
        Assert.Contains(reports!, x => x.GetProperty("targetKind").GetString() == "profile");
        Assert.Contains(reports!, x => x.GetProperty("targetKind").GetString() == "circle");
        Assert.Contains(reports!, x => x.GetProperty("targetKind").GetString() == "post");

        (await admin.PostAsJsonAsync("/api/admin/blocks", new { targetKind = "profile", targetId = donoAuth.UserId, blocked = true }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await dono.GetAsync("/api/auth/me")).StatusCode);
        var login = await dono.PostAsJsonAsync("/api/auth/login", new { email = donoAuth.Email, password = "senha-segura-1" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.SentEmails.AnyAsync(x => x.To == donoAuth.Email && x.Subject.Contains("bloqueada")));
        }

        (await admin.PostAsJsonAsync("/api/admin/blocks", new { targetKind = "profile", targetId = donoAuth.UserId, blocked = false }))
            .EnsureSuccessStatusCode();
        var donoNovo = _factory.CreateClient();
        var relogin = await donoNovo.PostAsJsonAsync("/api/auth/login", new { email = donoAuth.Email, password = "senha-segura-1" });
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
        var session = await relogin.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        donoNovo.WithToken(session!.Token!);

        (await admin.PostAsJsonAsync("/api/admin/blocks", new { targetKind = "circle", targetId = circleId, blocked = true }))
            .EnsureSuccessStatusCode();
        var discover = await reporter.GetFromJsonAsync<List<JsonElement>>("/api/circles/discover", Json);
        Assert.DoesNotContain(discover!, x => x.GetProperty("id").GetGuid() == circleId);
        var blockedDetail = await donoNovo.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}", Json);
        Assert.Equal("blocked", blockedDetail.GetProperty("status").GetString());

        (await admin.PostAsJsonAsync("/api/admin/blocks", new { targetKind = "circle", targetId = circleId, blocked = false }))
            .EnsureSuccessStatusCode();
        var actions = await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/actions", Json);
        Assert.Contains(actions!, x => x.GetProperty("action").GetString() == "block");
        Assert.Contains(actions!, x => x.GetProperty("action").GetString() == "unblock");
    }

    [Fact]
    public async Task CookieHttpOnly_SoNaWeb_NativoContinuaComBearer()
    {
        var nativo = _factory.CreateClient();
        var nativoAuth = await AuthHelper.RegisterAsync(nativo);
        Assert.False(string.IsNullOrWhiteSpace(nativoAuth.RefreshToken));
        Assert.False(nativo.DefaultRequestHeaders.Contains("X-Client"));
        var nativoLogin = await nativo.PostAsJsonAsync("/api/auth/login", new { email = nativoAuth.Email, password = "senha-segura-1" });
        Assert.False(HasRefreshCookie(nativoLogin));
        nativo.WithToken(nativoAuth.Token);
        Assert.Equal(HttpStatusCode.OK, (await nativo.GetAsync("/api/auth/me")).StatusCode);

        var web = _factory.CreateClient();
        web.DefaultRequestHeaders.Add("X-Client", "web");
        var email = $"web-{Guid.NewGuid():N}@exemplo.com";
        var register = await web.PostAsJsonAsync("/api/auth/register", new { email, password = "senha-segura-1", name = "Web" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.True(HasRefreshCookie(register));
        var payload = await register.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload!.RefreshToken));

        var cookieOnly = _factory.CreateClient();
        cookieOnly.DefaultRequestHeaders.Add("X-Client", "web");
        foreach (var cookie in register.Headers.GetValues("Set-Cookie"))
        {
            cookieOnly.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie.Split(';')[0]);
        }

        var refresh = await cookieOnly.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(rotated?.Token));
        cookieOnly.WithToken(rotated!.Token!);
        Assert.Equal(HttpStatusCode.OK, (await cookieOnly.GetAsync("/api/auth/me")).StatusCode);
    }

    private async Task<HttpClient> AdminClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin-sistema@exemplo.com", password = "senha-segura-1" });
        if (!login.IsSuccessStatusCode)
        {
            var auth = await AuthHelper.RegisterAsync(client, "admin-sistema@exemplo.com", name: "Admin");
            client.WithToken(auth.Token);
            return client;
        }

        var payload = await login.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        client.WithToken(payload!.Token!);
        return client;
    }

    private async Task<long> AdminStatsShortcuts()
    {
        var admin = await AdminClient();
        var stats = await admin.GetFromJsonAsync<JsonElement>("/api/admin/stats", Json);
        return stats.GetProperty("shortcutsTriggered").GetInt64();
    }

    private static bool HasRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
        && values.Any(item => item.Contains("circulos.refresh", StringComparison.OrdinalIgnoreCase)
                              && item.Contains("httponly", StringComparison.OrdinalIgnoreCase));
}
