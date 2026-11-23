using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class Rodada9Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada9Tests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Entrada_TagsOpcionaisFiltramEBuscaAcha()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var areas = await client.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync($"/api/life-areas/{diario}/entries", new
        {
            title = "Treino 5k",
            body = "Leve",
            tags = new[] { "corrida", "manhã" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var tags = doc.RootElement.GetProperty("tags").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(new[] { "corrida", "manhã" }, tags);
        var entryId = doc.RootElement.GetProperty("id").GetGuid();

        var filtered = await client.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diario}/entries?tag=corrida", Json);
        Assert.Contains(filtered!, x => x.GetProperty("id").GetGuid() == entryId);
        var other = await client.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diario}/entries?tag=vacina", Json);
        Assert.DoesNotContain(other!, x => x.GetProperty("id").GetGuid() == entryId);

        var search = await client.GetFromJsonAsync<JsonElement>("/api/search?q=corrida", Json);
        Assert.Contains(search.GetProperty("hits").EnumerateArray(), x => x.GetProperty("id").GetGuid() == entryId);

        var updated = await client.PutAsJsonAsync($"/api/life-areas/{diario}/entries/{entryId}", new
        {
            title = "Treino 5k",
            body = "Leve",
            tags = new[] { "noite" },
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await client.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diario}/entries", Json);
        Assert.Single(after!);
        var savedTags = after[0].GetProperty("tags").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(new[] { "noite" }, savedTags);
        Assert.DoesNotContain(savedTags, x => x == "corrida");
    }

    [Fact]
    public async Task Entrada_EdicaoDeTagsSobreposta_SubstituiDeVez()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var areas = await client.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync($"/api/life-areas/{diario}/entries", new { title = "Marca", tags = new[] { "a" } });
        var entryId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(
            a.PutAsJsonAsync($"/api/life-areas/{diario}/entries/{entryId}", new { title = "Marca", tags = new[] { "um" } }),
            b.PutAsJsonAsync($"/api/life-areas/{diario}/entries/{entryId}", new { title = "Marca", tags = new[] { "dois" } }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diario}/entries", Json);
        Assert.Single(list!);
        var tags = list[0].GetProperty("tags").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.True(tags.SequenceEqual(["um"]) || tags.SequenceEqual(["dois"]));
    }

    [Fact]
    public async Task Entrada_MesmaNosCirculosSemDuplicar_VisitanteNaoVe()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);
        var visitante = _factory.CreateClient();
        visitante.WithToken((await AuthHelper.RegisterAsync(visitante, name: "Visitante")).Token);

        var mine = await dono.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        var pessoalId = mine!.Single(x => x.GetProperty("name").GetString() == "Eu").GetProperty("id").GetGuid();
        var equipe = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Equipe", visibility = "public", discoverable = true }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var equipeId = equipe.GetProperty("id").GetGuid();
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{equipeId}/invites", new { email = membroAuth.Email, role = "editor" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await membro.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();

        var areas = await dono.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var created = await dono.PostAsJsonAsync($"/api/life-areas/{diario}/entries", new
        {
            title = "Corrida 200m",
            numberValue = 200,
            tags = new[] { "treino" },
            circleIds = new[] { pessoalId, equipeId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entryId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var noPessoal = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{pessoalId}", Json);
        var noEquipe = await membro.GetFromJsonAsync<JsonElement>($"/api/circles/{equipeId}", Json);
        Assert.Contains(noPessoal.GetProperty("sharedEntries").EnumerateArray(), x => x.GetProperty("id").GetGuid() == entryId);
        Assert.Contains(noEquipe.GetProperty("sharedEntries").EnumerateArray(), x => x.GetProperty("id").GetGuid() == entryId);
        Assert.Equal("Corrida 200m", noEquipe.GetProperty("sharedEntries")[0].GetProperty("title").GetString());
        Assert.Equal(donoAuth.UserId, noEquipe.GetProperty("sharedEntries")[0].GetProperty("authorUserId").GetGuid());

        var preview = await visitante.GetFromJsonAsync<JsonElement>($"/api/circles/{equipeId}", Json);
        Assert.Empty(preview.GetProperty("sharedEntries").EnumerateArray());
        Assert.Empty(preview.GetProperty("posts").EnumerateArray());

        (await dono.PutAsJsonAsync($"/api/life-areas/{diario}/entries/{entryId}", new
        {
            title = "Corrida 400m",
            numberValue = 400,
            tags = new[] { "treino" },
            circleIds = new[] { pessoalId, equipeId },
        })).EnsureSuccessStatusCode();
        var depoisPessoal = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{pessoalId}", Json);
        var depoisEquipe = await membro.GetFromJsonAsync<JsonElement>($"/api/circles/{equipeId}", Json);
        Assert.Equal("Corrida 400m", depoisPessoal.GetProperty("sharedEntries")[0].GetProperty("title").GetString());
        Assert.Equal("Corrida 400m", depoisEquipe.GetProperty("sharedEntries")[0].GetProperty("title").GetString());
        Assert.Equal(entryId, depoisEquipe.GetProperty("sharedEntries")[0].GetProperty("id").GetGuid());
        Assert.Single(depoisPessoal.GetProperty("sharedEntries").EnumerateArray());
        Assert.Single(depoisEquipe.GetProperty("sharedEntries").EnumerateArray());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.LifeEntries.CountAsync(x => x.Id == entryId));
        Assert.Equal(2, await db.LifeEntryShares.CountAsync(x => x.LifeEntryId == entryId));
        Assert.Equal(0, await db.CirclePosts.CountAsync(x => x.CircleId == pessoalId || x.CircleId == equipeId));
    }

    [Fact]
    public async Task Entrada_ShareSobrepostoNoMesmoCirculo_FicaUmaLigacao()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var circle = await (await client.PostAsJsonAsync("/api/circles", new { name = "Casa" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();
        var areas = await client.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync($"/api/life-areas/{diario}/entries", new { title = "Dose" });
        var entryId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/life-areas/{diario}/entries/{entryId}/share", new { circleIds = new[] { circleId } }),
            b.PostAsJsonAsync($"/api/life-areas/{diario}/entries/{entryId}/share", new { circleIds = new[] { circleId } }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.LifeEntryShares.CountAsync(x => x.LifeEntryId == entryId && x.CircleId == circleId));
    }

    [Fact]
    public async Task Atalho_CompartilhaNoCirculo_MembroVeDeQuemVeioERegistraNoMesmoLugar()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Cuidador");
        dono.WithToken(donoAuth.Token);
        var filha = _factory.CreateClient();
        var filhaAuth = await AuthHelper.RegisterAsync(filha, name: "Filha");
        filha.WithToken(filhaAuth.Token);

        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Idoso" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email = filhaAuth.Email, role = "editor" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await filha.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();

        var created = await dono.PostAsJsonAsync("/api/shortcuts", new { label = "Tomei o remédio" });
        var shortcutId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var shared = await dono.PostAsJsonAsync($"/api/shortcuts/{shortcutId}/share", new { circleId });
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);

        var homeFilha = await filha.GetFromJsonAsync<JsonElement>("/api/home", Json);
        var atalho = homeFilha.GetProperty("shortcuts").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == shortcutId);
        Assert.Equal("Cuidador", atalho.GetProperty("sharedByName").GetString());
        Assert.Equal(donoAuth.UserId, atalho.GetProperty("sharedByUserId").GetGuid());

        var run = await filha.PostAsync($"/api/shortcuts/{shortcutId}/run", null);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        var areasDono = await dono.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diarioDono = areasDono!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var entradasDono = await dono.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diarioDono}/entries", Json);
        Assert.Contains(entradasDono!, x => x.GetProperty("title").GetString() == "Tomei o remédio");

        var areasFilha = await filha.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diarioFilha = areasFilha!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var entradasFilha = await filha.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diarioFilha}/entries", Json);
        Assert.DoesNotContain(entradasFilha!, x => x.GetProperty("title").GetString() == "Tomei o remédio");

        var detalhe = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}", Json);
        Assert.Contains(detalhe.GetProperty("log").EnumerateArray(), x =>
            x.GetProperty("action").GetString() == "shortcut-run"
            && (x.GetProperty("detail").GetString() ?? "").Contains("Tomei o remédio"));

        var denied = await filha.PutAsJsonAsync($"/api/shortcuts/{shortcutId}", new { label = "Não posso" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var edited = await dono.PutAsJsonAsync($"/api/shortcuts/{shortcutId}", new { label = "Tomei as 8h" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var homeDepois = await filha.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Contains(
            homeDepois.GetProperty("shortcutNotices").EnumerateArray(),
            x => (x.GetProperty("detail").GetString() ?? "").Contains("Nome: Tomei o remédio -> Tomei as 8h"));
    }

    [Fact]
    public async Task Atalho_ShareSobreposto_FicaUmaLigacao()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var circle = await (await client.PostAsJsonAsync("/api/circles", new { name = "Casa" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync("/api/shortcuts", new { label = "Dose" });
        var shortcutId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/shortcuts/{shortcutId}/share", new { circleId }),
            b.PostAsJsonAsync($"/api/shortcuts/{shortcutId}/share", new { circleId }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.ShortcutShares.CountAsync(x => x.ShortcutId == shortcutId && x.CircleId == circleId));
    }

    [Fact]
    public async Task Atalho_DoisMembrosDisparamSobrepostos_DoisRegistrosNoDono()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Cuidado" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email = membroAuth.Email, role = "editor" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await membro.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();
        var shortcutId = (await (await dono.PostAsJsonAsync("/api/shortcuts", new { label = "Dose" })).Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("id").GetGuid();
        (await dono.PostAsJsonAsync($"/api/shortcuts/{shortcutId}/share", new { circleId })).EnsureSuccessStatusCode();

        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(membroAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsync($"/api/shortcuts/{shortcutId}/run", null),
            b.PostAsync($"/api/shortcuts/{shortcutId}/run", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        var areas = await dono.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        var entradas = await dono.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diario}/entries", Json);
        Assert.Equal(2, entradas!.Count(x => x.GetProperty("title").GetString() == "Dose"));
    }

    [Fact]
    public async Task Lembrete_EmailOpcional_PadraoOffESobrepostoMandaUm()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var start = DateTime.UtcNow.AddHours(-2);
        (await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Remédio das oito",
            startAt = start,
            recurrence = new { frequency = "none", interval = 1 },
        })).EnsureSuccessStatusCode();

        (await client.GetAsync("/api/home")).EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.SentEmails.CountAsync(x => x.To == auth.Email && x.Subject.Contains("Remédio das oito")));
        }

        var pref = await client.PutAsJsonAsync("/api/reminders/email-preference", new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, pref.StatusCode);
        Assert.True((await pref.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("enabled").GetBoolean());

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var homes = await Task.WhenAll(a.GetAsync("/api/home"), b.GetAsync("/api/home"));
        Assert.All(homes, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        var sentFlags = 0;
        foreach (var home in homes)
        {
            using var doc = JsonDocument.Parse(await home.Content.ReadAsStringAsync());
            if (doc.RootElement.GetProperty("reminderEmailsSent").EnumerateArray().Any(x => x.GetString() == "Remédio das oito"))
            {
                sentFlags++;
            }
        }

        Assert.Equal(1, sentFlags);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.SentEmails.CountAsync(x => x.To == auth.Email && x.Subject.Contains("Remédio das oito")));
        }

        var again = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.DoesNotContain(again.GetProperty("reminderEmailsSent").EnumerateArray(), x => x.GetString() == "Remédio das oito");
    }

    [Fact]
    public async Task Lembrete_EmailEmFerias_NaoEnvia()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        (await client.PutAsJsonAsync("/api/reminders/email-preference", new { enabled = true })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/reminders/vacation", new { until = DateTime.UtcNow.AddDays(3) })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Durante férias",
            startAt = DateTime.UtcNow.AddHours(-1),
            recurrence = new { frequency = "none", interval = 1 },
        })).EnsureSuccessStatusCode();
        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Empty(home.GetProperty("reminderEmailsSent").EnumerateArray());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.SentEmails.CountAsync(x => x.To == auth.Email && x.Subject.Contains("Durante férias")));
    }
}
