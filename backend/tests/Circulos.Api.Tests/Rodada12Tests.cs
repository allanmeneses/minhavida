using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class Rodada12Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada12Tests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Localizacao_RedigeCoordenadaEmTextoDeLog()
    {
        Assert.DoesNotContain("-23.55", LocationPrivacy.Redact("lat=-23.55&lng=-46.63"));
        Assert.False(ReminderChannels.SupportsSms);
        Assert.Equal(new[] { ReminderChannels.App, ReminderChannels.Email }, ReminderChannels.Allowed);
    }

    [Fact]
    public async Task Localizacao_NaoPersisteLatLongDoUsuario_NemEmLog()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Geo");
        dono.WithToken(donoAuth.Token);
        (await dono.PutAsJsonAsync("/api/profile", new
        {
            displayName = "Geo",
            findableByLocation = true,
            latitude = -23.55052,
            longitude = -46.63331,
        })).EnsureSuccessStatusCode();

        var profile = await dono.GetFromJsonAsync<JsonElement>("/api/profile", Json);
        Assert.False(profile.TryGetProperty("latitude", out _));
        Assert.False(profile.TryGetProperty("longitude", out _));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Profiles.SingleAsync(item => item.UserId == donoAuth.UserId);
            Assert.Null(stored.Latitude);
            Assert.Null(stored.Longitude);
        }

        (await dono.GetAsync("/api/search/nearby?lat=-23.55052&lng=-46.63331&radiusKm=5")).EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.SearchQueries.AnyAsync(item => item.Term.Contains("23.55052") || item.Term.Contains("46.63331")));
            Assert.False(await db.CircleLogs.AnyAsync(item => item.Detail.Contains("23.55052") || item.Detail.Contains("46.63331")));
            Assert.False(await db.SentEmails.AnyAsync(item => item.Body.Contains("23.55052") || item.Subject.Contains("23.55052")));
            var stored = await db.Profiles.SingleAsync(item => item.UserId == donoAuth.UserId);
            Assert.Null(stored.Latitude);
            Assert.Null(stored.Longitude);
        }
    }

    [Fact]
    public async Task Lembrete_Circulo_SoMembrosRecebemNoAppEEmail_SemSms()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);
        var deFora = _factory.CreateClient();
        var deForaAuth = await AuthHelper.RegisterAsync(deFora, name: "Fora");
        deFora.WithToken(deForaAuth.Token);

        var familia = await FamiliaId(dono);
        await InviteAsync(dono, membro, membroAuth.Email, familia, "viewer");
        (await dono.PostAsJsonAsync("/api/reminders", new
        {
            title = "Remédio da casa",
            startAt = DateTime.UtcNow.AddHours(-2),
            circleId = familia,
            recurrence = new { frequency = "none", interval = 1 },
        })).EnsureSuccessStatusCode();

        (await membro.PutAsJsonAsync("/api/reminders/email-preference", new { enabled = true })).EnsureSuccessStatusCode();
        (await deFora.PutAsJsonAsync("/api/reminders/email-preference", new { enabled = true })).EnsureSuccessStatusCode();

        var membroHome = await membro.GetFromJsonAsync<JsonElement>("/api/home", Json);
        var deForaHome = await deFora.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Contains(membroHome.GetProperty("overdue").EnumerateArray(), x => x.GetProperty("title").GetString() == "Remédio da casa");
        Assert.DoesNotContain(deForaHome.GetProperty("overdue").EnumerateArray(), x => x.GetProperty("title").GetString() == "Remédio da casa");
        Assert.Contains(membroHome.GetProperty("reminderEmailsSent").EnumerateArray(), x => x.GetString() == "Remédio da casa");
        Assert.DoesNotContain(deForaHome.GetProperty("reminderEmailsSent").EnumerateArray(), x => x.GetString() == "Remédio da casa");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SentEmails.CountAsync(x => x.To == membroAuth.Email && x.Subject.Contains("Remédio da casa")));
        Assert.Equal(0, await db.SentEmails.CountAsync(x => x.To == deForaAuth.Email && x.Subject.Contains("Remédio da casa")));
        var mailed = await db.SentEmails.SingleAsync(x => x.To == membroAuth.Email && x.Subject.Contains("Remédio da casa"));
        Assert.Contains("Sem SMS", mailed.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await membro.GetAsync("/api/reminders/sms")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await membro.PostAsJsonAsync("/api/reminders/sms", new { phone = "11999999999" })).StatusCode);
    }

    [Fact]
    public async Task Lembrete_DoisMembrosSobrepostos_CadaUmRecebeUmEmail()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var a = _factory.CreateClient();
        var aAuth = await AuthHelper.RegisterAsync(a, name: "Ana");
        a.WithToken(aAuth.Token);
        var b = _factory.CreateClient();
        var bAuth = await AuthHelper.RegisterAsync(b, name: "Bia");
        b.WithToken(bAuth.Token);
        var familia = await FamiliaId(dono);
        await InviteAsync(dono, a, aAuth.Email, familia, "viewer");
        await InviteAsync(dono, b, bAuth.Email, familia, "viewer");
        (await dono.PostAsJsonAsync("/api/reminders", new
        {
            title = "Dose compartilhada",
            startAt = DateTime.UtcNow.AddHours(-1),
            circleId = familia,
            recurrence = new { frequency = "none", interval = 1 },
        })).EnsureSuccessStatusCode();
        (await a.PutAsJsonAsync("/api/reminders/email-preference", new { enabled = true })).EnsureSuccessStatusCode();
        (await b.PutAsJsonAsync("/api/reminders/email-preference", new { enabled = true })).EnsureSuccessStatusCode();

        var a2 = _factory.CreateClient();
        a2.WithToken(aAuth.Token);
        var homes = await Task.WhenAll(a.GetAsync("/api/home"), a2.GetAsync("/api/home"), b.GetAsync("/api/home"));
        Assert.All(homes, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SentEmails.CountAsync(x => x.To == aAuth.Email && x.Subject.Contains("Dose compartilhada")));
        Assert.Equal(1, await db.SentEmails.CountAsync(x => x.To == bAuth.Email && x.Subject.Contains("Dose compartilhada")));
    }

    [Fact]
    public async Task FeedGeral_EAnuncios_NaoExistem()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/ads")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/anúncios")).StatusCode);
    }

    [Fact]
    public async Task Calendario_NuncaGravaNoAparelho()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var post = await client.PostAsJsonAsync("/api/calendar", new { title = "Não gravar" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        var put = await client.PutAsJsonAsync("/api/calendar", new { title = "Não gravar" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        var delete = await client.DeleteAsync("/api/calendar");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
        var body = await post.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("nunca grava", body.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IdeiasDePresente_EditorAdiciona_ViewerRecebe403()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var editor = _factory.CreateClient();
        var editorAuth = await AuthHelper.RegisterAsync(editor, name: "Editor");
        editor.WithToken(editorAuth.Token);
        var viewer = _factory.CreateClient();
        var viewerAuth = await AuthHelper.RegisterAsync(viewer, name: "Viewer");
        viewer.WithToken(viewerAuth.Token);
        var familia = await FamiliaId(dono);
        await InviteAsync(dono, editor, editorAuth.Email, familia, "editor");
        await InviteAsync(dono, viewer, viewerAuth.Email, familia, "viewer");
        var reminder = await (await dono.PostAsJsonAsync("/api/reminders", new
        {
            title = "Aniversário compartilhado",
            kind = "birthday",
            startAt = DateTime.UtcNow.Date.AddDays(20),
            personName = "Ana",
            giftIdeas = new[] { "Livro" },
            circleId = familia,
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var reminderId = reminder.GetProperty("id").GetGuid();

        var added = await editor.PostAsJsonAsync($"/api/circles/{familia}/gift-ideas/{reminderId}/ideas", new { idea = "Planta" });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var forbidden = await viewer.PostAsJsonAsync($"/api/circles/{familia}/gift-ideas/{reminderId}/ideas", new { idea = "Chocolate" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var message = await forbidden.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("visualiza", message.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
        var putForbidden = await viewer.PutAsJsonAsync($"/api/circles/{familia}/gift-ideas/{reminderId}", new { ideas = new[] { "Só ver" } });
        Assert.Equal(HttpStatusCode.Forbidden, putForbidden.StatusCode);

        var ideas = await viewer.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{familia}/gift-ideas", Json);
        var shared = ideas!.Single(item => item.GetProperty("reminderId").GetGuid() == reminderId);
        var list = shared.GetProperty("ideas").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("Livro", list);
        Assert.Contains("Planta", list);
        Assert.DoesNotContain("Chocolate", list);
        Assert.DoesNotContain("Só ver", list);
    }

    [Fact]
    public async Task IdeiasDePresente_DoisEditoresSobrepostos_AsDuasIdeiasFicam()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var editor = _factory.CreateClient();
        var editorAuth = await AuthHelper.RegisterAsync(editor, name: "Editor");
        editor.WithToken(editorAuth.Token);
        var familia = await FamiliaId(dono);
        await InviteAsync(dono, editor, editorAuth.Email, familia, "editor");
        var reminder = await (await dono.PostAsJsonAsync("/api/reminders", new
        {
            title = "Ideias juntas",
            kind = "birthday",
            startAt = DateTime.UtcNow.Date.AddDays(10),
            giftIdeas = new[] { "Base" },
            circleId = familia,
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var reminderId = reminder.GetProperty("id").GetGuid();
        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(editorAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/circles/{familia}/gift-ideas/{reminderId}/ideas", new { idea = "Caneca" }),
            b.PostAsJsonAsync($"/api/circles/{familia}/gift-ideas/{reminderId}/ideas", new { idea = "Meia" }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        var ideas = await dono.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{familia}/gift-ideas", Json);
        var list = ideas!.Single().GetProperty("ideas").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("Base", list);
        Assert.Contains("Caneca", list);
        Assert.Contains("Meia", list);
    }

    [Fact]
    public async Task Diario_BuscaPorPalavraChave_RespeitaCirculo()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);
        var deFora = _factory.CreateClient();
        deFora.WithToken((await AuthHelper.RegisterAsync(deFora, name: "Fora")).Token);
        var familia = await FamiliaId(dono);
        await InviteAsync(dono, membro, membroAuth.Email, familia, "viewer");

        var areas = await dono.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();
        (await dono.PostAsJsonAsync($"/api/life-areas/{diario}/pages", new { title = "Página secreta", body = "segredo-xyz no diário solo" })).EnsureSuccessStatusCode();
        (await dono.PostAsJsonAsync($"/api/life-areas/{diario}/entries", new
        {
            title = "Entrada compartilhada-xyz",
            body = "texto visível no círculo",
            circleIds = new[] { familia },
        })).EnsureSuccessStatusCode();

        var ownSearch = await dono.GetFromJsonAsync<JsonElement>($"/api/life-areas/{diario}/search?q=segredo-xyz", Json);
        Assert.Contains(ownSearch.EnumerateArray(), x => x.GetProperty("title").GetString() == "Página secreta");
        var unified = await dono.GetFromJsonAsync<JsonElement>("/api/search?q=segredo-xyz&type=diary", Json);
        Assert.Contains(unified.GetProperty("hits").EnumerateArray(), x => x.GetProperty("title").GetString() == "Página secreta");

        var membroBusca = await membro.GetFromJsonAsync<JsonElement>("/api/search?q=compartilhada-xyz&type=diary", Json);
        Assert.Contains(membroBusca.GetProperty("hits").EnumerateArray(), x => x.GetProperty("title").GetString()!.Contains("compartilhada-xyz"));
        var membroSegredo = await membro.GetFromJsonAsync<JsonElement>("/api/search?q=segredo-xyz&type=diary", Json);
        Assert.DoesNotContain(membroSegredo.GetProperty("hits").EnumerateArray(), x => x.GetProperty("title").GetString() == "Página secreta");
        Assert.Equal(HttpStatusCode.NotFound, (await membro.GetAsync($"/api/life-areas/{diario}/search?q=segredo-xyz")).StatusCode);

        var deForaBusca = await deFora.GetFromJsonAsync<JsonElement>("/api/search?q=compartilhada-xyz&type=diary", Json);
        Assert.DoesNotContain(deForaBusca.GetProperty("hits").EnumerateArray(), x => x.GetProperty("title").GetString()!.Contains("compartilhada-xyz"));
    }

    [Fact]
    public async Task Votacao_SemQuorum_UmVotoNoPrazoElege()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var ana = _factory.CreateClient();
        var anaAuth = await AuthHelper.RegisterAsync(ana, name: "Ana");
        ana.WithToken(anaAuth.Token);
        var bia = _factory.CreateClient();
        var biaAuth = await AuthHelper.RegisterAsync(bia, name: "Bia");
        bia.WithToken(biaAuth.Token);
        var created = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Sem quorum", visibility = "public" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = created.GetProperty("id").GetGuid();
        await InviteAsync(dono, ana, anaAuth.Email, circleId, "viewer");
        await InviteAsync(dono, bia, biaAuth.Email, circleId, "viewer");
        (await dono.DeleteAsync($"/api/circles/{circleId}/membership")).EnsureSuccessStatusCode();
        (await ana.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();

        var stillOpen = await ana.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("open", stillOpen.GetProperty("status").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var open = await db.SuccessionVotes.SingleAsync(item => item.CircleId == circleId);
            open.DeadlineAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var decided = await ana.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("decided", decided.GetProperty("status").GetString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(item => item.Id == circleId);
            Assert.Equal(anaAuth.UserId, stored.OwnerUserId);
            Assert.NotNull(stored.OwnerElectedAt);
        }
    }

    [Fact]
    public async Task Votacao_SemQuorum_DurantePrazoNaoFechaSemTodosVotarem()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var ana = _factory.CreateClient();
        var anaAuth = await AuthHelper.RegisterAsync(ana, name: "Ana");
        ana.WithToken(anaAuth.Token);
        var bia = _factory.CreateClient();
        var biaAuth = await AuthHelper.RegisterAsync(bia, name: "Bia");
        bia.WithToken(biaAuth.Token);
        var caio = _factory.CreateClient();
        var caioAuth = await AuthHelper.RegisterAsync(caio, name: "Caio");
        caio.WithToken(caioAuth.Token);
        var created = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Tres votos", visibility = "public" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = created.GetProperty("id").GetGuid();
        await InviteAsync(dono, ana, anaAuth.Email, circleId, "viewer");
        await InviteAsync(dono, bia, biaAuth.Email, circleId, "viewer");
        await InviteAsync(dono, caio, caioAuth.Email, circleId, "viewer");
        (await dono.DeleteAsync($"/api/circles/{circleId}/membership")).EnsureSuccessStatusCode();
        (await ana.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();
        (await bia.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();
        var open = await ana.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("open", open.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AdminEleito_SubstituicaoAntesEDepoisDe30Dias()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var ana = _factory.CreateClient();
        var anaAuth = await AuthHelper.RegisterAsync(ana, name: "Ana");
        ana.WithToken(anaAuth.Token);
        var bia = _factory.CreateClient();
        var biaAuth = await AuthHelper.RegisterAsync(bia, name: "Bia");
        bia.WithToken(biaAuth.Token);
        var created = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Troca dono", visibility = "public" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = created.GetProperty("id").GetGuid();
        await InviteAsync(dono, ana, anaAuth.Email, circleId, "viewer");
        await InviteAsync(dono, bia, biaAuth.Email, circleId, "viewer");
        (await dono.DeleteAsync($"/api/circles/{circleId}/membership")).EnsureSuccessStatusCode();
        (await ana.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();
        (await bia.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();

        var decided = await ana.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("decided", decided.GetProperty("status").GetString());
        Assert.Equal("O dono eleito só pode ser substituído após 30 dias da eleição.", decided.GetProperty("replacementBlockedMessage").GetString());
        Assert.False(decided.GetProperty("canRequestReplacement").GetBoolean());

        var tooSoon = await bia.PostAsync($"/api/circles/{circleId}/succession/replace", null);
        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);
        var tooSoonBody = await tooSoon.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("O dono eleito só pode ser substituído após 30 dias da eleição.", tooSoonBody.GetProperty("error").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(item => item.Id == circleId);
            stored.OwnerElectedAt = DateTime.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
        }

        var a = _factory.CreateClient();
        a.WithToken(biaAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(anaAuth.Token);
        var overlap = await Task.WhenAll(
            a.PostAsync($"/api/circles/{circleId}/succession/replace", null),
            b.PostAsync($"/api/circles/{circleId}/succession/replace", null));
        Assert.All(overlap, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));

        var open = await bia.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("open", open.GetProperty("status").GetString());
        using var final = _factory.Services.CreateScope();
        var dbFinal = final.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await dbFinal.SuccessionVotes.CountAsync(item => item.CircleId == circleId));
        Assert.Equal(1, await dbFinal.SuccessionVotes.CountAsync(item => item.CircleId == circleId && item.Status == SuccessionVoteStatus.Open));
    }

    private async Task<Guid> FamiliaId(HttpClient client)
    {
        var circles = await client.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        return circles!.Single(x => x.GetProperty("name").GetString() == "Família").GetProperty("id").GetGuid();
    }

    private static async Task InviteAsync(HttpClient owner, HttpClient member, string email, Guid circleId, string role)
    {
        var invite = await (await owner.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email, role }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await member.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { }))
            .EnsureSuccessStatusCode();
    }
}
