using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class RecapTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public RecapTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Recap_Perfil_SoMostraFluxosComUso()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/recap/me", Json);
        var initial = empty.GetProperty("flows").EnumerateArray().ToList();
        Assert.Contains(initial, x => x.GetProperty("key").GetString() == "circles" && x.GetProperty("count").GetInt32() == 2);
        Assert.DoesNotContain(initial, x => x.GetProperty("key").GetString() == "vaccine");

        var areas = await client.GetFromJsonAsync<List<AreaDto>>("/api/life-areas", Json);
        var vacinas = areas!.Single(x => x.Kind == "vaccine");
        (await client.PostAsJsonAsync($"/api/life-areas/{vacinas.Id}/entries", new
        {
            title = "Hepatite B",
            occurredOn = "2026-02-01",
            location = "UBS Centro",
        })).EnsureSuccessStatusCode();

        var recap = await client.GetFromJsonAsync<JsonElement>("/api/recap/me", Json);
        var flows = recap.GetProperty("flows").EnumerateArray().ToList();
        Assert.Contains(flows, x => x.GetProperty("key").GetString() == "vaccine" && x.GetProperty("count").GetInt32() == 1);
        Assert.Contains(flows, x => x.GetProperty("key").GetString() == "circles" && x.GetProperty("count").GetInt32() == 2);
        Assert.DoesNotContain(flows, x => x.GetProperty("key").GetString() == "diary");
    }

    [Fact]
    public async Task Recap_Circulo_SoAHistoriaDaPessoa_NaoVazaPostDoOutro()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);

        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Corrida", kind = "temporary" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = membroAuth.Email, role = "editor" }))
            .Content.ReadFromJsonAsync<InviteDto>(Json);
        (await membro.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();

        (await dono.PostAsJsonAsync($"/api/circles/{circle.Id}/posts", new { body = "Segredo do dono" })).EnsureSuccessStatusCode();
        (await membro.PostAsJsonAsync($"/api/circles/{circle.Id}/posts", new { body = "Minha volta de 5k" })).EnsureSuccessStatusCode();

        var recap = await membro.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}/recap", Json);
        var titles = recap.GetProperty("myTitles").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Minha volta de 5k", titles);
        Assert.DoesNotContain("Segredo do dono", titles);
        Assert.Equal("Corrida", recap.GetProperty("name").GetString());
        Assert.Equal("temporary", recap.GetProperty("kind").GetString());
        Assert.Contains(recap.GetProperty("flows").EnumerateArray(), x => x.GetProperty("key").GetString() == "posts" && x.GetProperty("count").GetInt32() == 1);
    }

    [Fact]
    public async Task Recap_CompartilhaHistoriaPropria_VisitanteNaoVeOsOutros()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Curso", kind = "temporary" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/posts", new { body = "Terminei o modulo" })).EnsureSuccessStatusCode();

        var share = await dono.PostAsync($"/api/circles/{circle.Id}/recap/share", null);
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        using var created = JsonDocument.Parse(await share.Content.ReadAsStringAsync());
        var token = created.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var visitante = _factory.CreateClient();
        var publico = await visitante.GetFromJsonAsync<JsonElement>($"/api/public/recaps/{token}", Json);
        Assert.Contains(publico.GetProperty("myTitles").EnumerateArray(), x => x.GetString() == "Terminei o modulo");
        Assert.Equal("Dono", publico.GetProperty("displayName").GetString());
        Assert.DoesNotContain(publico.GetRawText(), "e-mail", StringComparison.OrdinalIgnoreCase);

        (await dono.PostAsJsonAsync($"/api/circles/{circle.Id}/posts", new { body = "Depois do share" })).EnsureSuccessStatusCode();
        var depois = await visitante.GetFromJsonAsync<JsonElement>($"/api/public/recaps/{token}", Json);
        Assert.DoesNotContain(depois.GetProperty("myTitles").EnumerateArray().Select(x => x.GetString()), x => x == "Depois do share");
        Assert.Contains(depois.GetProperty("myTitles").EnumerateArray().Select(x => x.GetString()), x => x == "Terminei o modulo");
    }

    [Fact]
    public async Task Recap_ShareSobreposto_GeraDoisTokens()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Curso", kind = "temporary" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/posts", new { body = "Minha historia" })).EnsureSuccessStatusCode();
        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsync($"/api/circles/{circle.Id}/recap/share", null),
            b.PostAsync($"/api/circles/{circle.Id}/recap/share", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        var tokens = new HashSet<string>();
        foreach (var response in responses)
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            tokens.Add(doc.RootElement.GetProperty("token").GetString()!);
        }

        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public async Task Arquivo_TemporarioGeraRecap_DuradouroFalha()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var mine = await client.GetFromJsonAsync<List<CircleDto>>("/api/circles", Json);
        var lasting = mine!.Single(x => x.Name == "Familia");
        var denied = await client.PostAsync($"/api/circles/{lasting!.Id}/archive", null);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        var temp = await (await client.PostAsJsonAsync("/api/circles", new { name = "Evento", kind = "temporary" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        (await client.PostAsJsonAsync($"/api/circles/{temp!.Id}/posts", new { body = "Foi bom" })).EnsureSuccessStatusCode();
        var archived = await client.PostAsync($"/api/circles/{temp.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        using var doc = JsonDocument.Parse(await archived.Content.ReadAsStringAsync());
        Assert.Equal("archived", doc.RootElement.GetProperty("status").GetString());

        var recap = await client.GetFromJsonAsync<JsonElement>($"/api/circles/{temp.Id}/recap", Json);
        Assert.Equal("archived", recap.GetProperty("status").GetString());
        Assert.Contains(recap.GetProperty("myTitles").EnumerateArray(), x => x.GetString() == "Foi bom");
    }

    [Fact]
    public async Task ArquivoSobreposto_FicaArquivadoUmaVez()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono);
        dono.WithToken(donoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Diario", kind = "daily" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsync($"/api/circles/{circle!.Id}/archive", null),
            b.PostAsync($"/api/circles/{circle.Id}/archive", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        Assert.Equal("archived", detail.GetProperty("status").GetString());
        var log = detail.GetProperty("log").EnumerateArray().Count(x => x.GetProperty("action").GetString() == "archived");
        Assert.Equal(1, log);
    }

    [Fact]
    public async Task Arquivo_ViewerRecebe403()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono);
        dono.WithToken(donoAuth.Token);
        var viewer = _factory.CreateClient();
        var viewerAuth = await AuthHelper.RegisterAsync(viewer, name: "Visitante");
        viewer.WithToken(viewerAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Curso", kind = "temporary" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = viewerAuth.Email, role = "viewer" }))
            .Content.ReadFromJsonAsync<InviteDto>(Json);
        (await viewer.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/circles/{circle.Id}/archive", null)).StatusCode);
    }

    [Fact]
    public async Task Recap_VisitanteDoCirculoNaoLe()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono)).Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Privado", visibility = "private" }))
            .Content.ReadFromJsonAsync<CircleDto>(Json);
        var visitante = _factory.CreateClient();
        visitante.WithToken((await AuthHelper.RegisterAsync(visitante, name: "Visitante")).Token);
        var recap = await visitante.GetAsync($"/api/circles/{circle!.Id}/recap");
        Assert.Equal(HttpStatusCode.NotFound, recap.StatusCode);
    }

    private sealed record AreaDto(Guid Id, string Kind);
    private sealed record CircleDto(Guid Id, string? Name);
    private sealed record InviteDto(string Token);
}
