using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Circulos.Api.Tests;

public sealed class Rodada11Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada11Tests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cadastro_CriaCirculosEuEFamiliaComTresExemplos()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client, name: "Nova");
        client.WithToken(auth.Token);

        var circles = await client.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        Assert.Equal(2, circles!.Count);
        Assert.Contains(circles, x => x.GetProperty("name").GetString() == "Eu");
        Assert.Contains(circles, x => x.GetProperty("name").GetString() == "Família");

        var examples = await client.GetFromJsonAsync<List<JsonElement>>("/api/onboarding", Json);
        Assert.Equal(3, examples!.Count);
        Assert.All(examples, x => Assert.True(x.GetProperty("pending").GetBoolean()));
        Assert.Contains(examples, x => x.GetProperty("kind").GetString() == "diary");
        Assert.Contains(examples, x => x.GetProperty("kind").GetString() == "memory");
        Assert.Contains(examples, x => x.GetProperty("kind").GetString() == "post");

        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(3, home.GetProperty("onboarding").GetArrayLength());
        Assert.Equal(0, home.GetProperty("routineStreakDays").GetInt32());
        Assert.Equal(2, home.GetProperty("activeCircleCount").GetInt32());
        var plan = await client.GetFromJsonAsync<JsonElement>("/api/plan", Json);
        Assert.Equal(0, plan.GetProperty("circleCount").GetInt32());
    }

    [Fact]
    public async Task Onboarding_ConfirmarEDescartarSobrepostos_DecideUmaVez()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var examples = await client.GetFromJsonAsync<List<JsonElement>>("/api/onboarding", Json);
        var diary = examples!.Single(x => x.GetProperty("kind").GetString() == "diary");
        var id = diary.GetProperty("id").GetGuid();

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/onboarding/{id}", new { confirm = true }),
            b.PostAsJsonAsync($"/api/onboarding/{id}", new { confirm = false }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.OnboardingExamples.SingleAsync(x => x.Id == id);
        Assert.True(stored.ConfirmedAt is not null ^ stored.DiscardedAt is not null);
        var entries = await db.LifeEntries.CountAsync(x => x.UserId == auth.UserId && x.Title == stored.Title);
        Assert.Equal(stored.ConfirmedAt is null ? 0 : 1, entries);
    }

    [Fact]
    public async Task Onboarding_ConfirmaOsTres_FechaECriaConteudo()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var examples = await client.GetFromJsonAsync<List<JsonElement>>("/api/onboarding", Json);
        foreach (var example in examples!)
        {
            Assert.Equal(
                HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"/api/onboarding/{example.GetProperty("id").GetGuid()}", new { confirm = true })).StatusCode);
        }

        var after = await client.GetFromJsonAsync<List<JsonElement>>("/api/onboarding", Json);
        Assert.All(after!, x => Assert.False(x.GetProperty("pending").GetBoolean()));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
        Assert.NotNull(user.OnboardingCompletedAt);
        Assert.Equal(1, await db.LifeEntries.CountAsync(x => x.UserId == auth.UserId && x.Title == "Como foi o meu dia"));
        Assert.Equal(1, await db.Memories.CountAsync(x => x.AuthorUserId == auth.UserId && x.Title == "Uma data que importa"));
        Assert.Equal(1, await db.CirclePosts.CountAsync(x => x.AuthorUserId == auth.UserId));
        var recap = await client.GetFromJsonAsync<JsonElement>("/api/recap/me", Json);
        Assert.Contains(recap.GetProperty("flows").EnumerateArray(), x => x.GetProperty("key").GetString() == "circles" && x.GetProperty("count").GetInt32() == 2);
    }

    [Fact]
    public async Task CirculosProntos_NaoComemCota_DescartarDeixaVazios()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
            Assert.Equal(0, user.OwnedCircleCount);
            Assert.Equal(2, await db.Circles.CountAsync(x => x.OwnerUserId == auth.UserId && x.IsStarter));
        }

        var emptyRecap = await client.GetFromJsonAsync<JsonElement>("/api/recap/me", Json);
        Assert.DoesNotContain(emptyRecap.GetProperty("flows").EnumerateArray(), x => x.GetProperty("key").GetString() == "circles");

        var examples = await client.GetFromJsonAsync<List<JsonElement>>("/api/onboarding", Json);
        foreach (var example in examples!)
        {
            Assert.Equal(
                HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync($"/api/onboarding/{example.GetProperty("id").GetGuid()}", new { confirm = false })).StatusCode);
        }

        var circles = await client.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        Assert.Contains(circles!, x => x.GetProperty("name").GetString() == "Eu");
        Assert.Contains(circles, x => x.GetProperty("name").GetString() == "Família");
        var euId = circles.Single(x => x.GetProperty("name").GetString() == "Eu").GetProperty("id").GetGuid();
        var familiaId = circles.Single(x => x.GetProperty("name").GetString() == "Família").GetProperty("id").GetGuid();
        var eu = await client.GetFromJsonAsync<JsonElement>($"/api/circles/{euId}", Json);
        var familia = await client.GetFromJsonAsync<JsonElement>($"/api/circles/{familiaId}", Json);
        Assert.Empty(eu.GetProperty("posts").EnumerateArray());
        Assert.Empty(familia.GetProperty("posts").EnumerateArray());

        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(2, home.GetProperty("activeCircleCount").GetInt32());
        Assert.Equal(0, home.GetProperty("onboarding").GetArrayLength());
        var afterDiscard = await client.GetFromJsonAsync<JsonElement>("/api/recap/me", Json);
        Assert.DoesNotContain(afterDiscard.GetProperty("flows").EnumerateArray(), x => x.GetProperty("key").GetString() == "circles");

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/circles", new { name = "Um" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/circles", new { name = "Dois" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/circles", new { name = "Tres" })).StatusCode);
        var fourth = await client.PostAsJsonAsync("/api/circles", new { name = "Quatro" });
        Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
        Assert.Contains("3 círculos", await fourth.Content.ReadAsStringAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
            Assert.Equal(3, user.OwnedCircleCount);
            Assert.Equal(5, await db.Circles.CountAsync(x => x.OwnerUserId == auth.UserId && x.Status == CircleStatus.Active));
        }

        home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(5, home.GetProperty("activeCircleCount").GetInt32());
        var used = await client.GetFromJsonAsync<JsonElement>("/api/recap/me", Json);
        Assert.Contains(used.GetProperty("flows").EnumerateArray(), x => x.GetProperty("key").GetString() == "circles" && x.GetProperty("count").GetInt32() == 3);
    }

    [Fact]
    public async Task Google_DesligadoPorPadrao_GetFalseEPost404()
    {
        var client = _factory.CreateClient();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/auth/google", Json);
        Assert.False(status.GetProperty("enabled").GetBoolean());
        var login = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "qualquer" });
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
    }

    [Fact]
    public async Task NesteDia_ERetrospectivaMensal_AparecemNoPainelEAckSome()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastYear = today.AddYears(-1);
        var previousMonth = today.AddDays(1 - today.Day).AddDays(-1);

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/memories", new
            {
                title = "Aniversário antigo",
                body = "Mesmo dia",
                occurredOn = lastYear.ToString("yyyy-MM-dd"),
            })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/memories", new
            {
                title = "Do mês passado",
                body = "Surpresa",
                occurredOn = previousMonth.ToString("yyyy-MM-dd"),
            })).StatusCode);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Contains(
            home.GetProperty("onThisDay").EnumerateArray(),
            x => x.GetProperty("title").GetString() == "Aniversário antigo" && x.GetProperty("year").GetInt32() == lastYear.Year);
        Assert.Equal(previousMonth.Year, home.GetProperty("monthlySurprise").GetProperty("year").GetInt32());
        Assert.Equal(previousMonth.Month, home.GetProperty("monthlySurprise").GetProperty("month").GetInt32());

        var onThisDay = await client.GetFromJsonAsync<List<JsonElement>>("/api/memories/on-this-day", Json);
        Assert.Contains(onThisDay!, x => x.GetProperty("title").GetString() == "Aniversário antigo");

        var album = await client.GetFromJsonAsync<JsonElement>(
            $"/api/memories/album?year={previousMonth.Year}&month={previousMonth.Month}",
            Json);
        Assert.Contains(album.GetProperty("items").EnumerateArray(), x => x.GetProperty("title").GetString() == "Do mês passado");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/recap/monthly/ack", null)).StatusCode);
        var afterAck = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(JsonValueKind.Null, afterAck.GetProperty("monthlySurprise").ValueKind);
    }

    [Fact]
    public async Task ConvitePorLink_TextoProntoEDoisAceitamSobrepostos()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circles = await dono.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        var familia = circles!.Single(x => x.GetProperty("name").GetString() == "Família").GetProperty("id").GetGuid();

        var created = await dono.PostAsJsonAsync($"/api/circles/{familia}/invite-link", new { });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var invite = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = invite.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Contains("/i/", invite.GetProperty("shareUrl").GetString(), StringComparison.Ordinal);
        Assert.Contains("Entre no círculo Família", invite.GetProperty("shareText").GetString(), StringComparison.Ordinal);

        var anon = _factory.CreateClient();
        var preview = await anon.GetFromJsonAsync<JsonElement>($"/api/circles/invites/{token}", Json);
        Assert.Equal("Família", preview.GetProperty("circleName").GetString());
        Assert.True(preview.GetProperty("openLink").GetBoolean());

        var a = _factory.CreateClient();
        var aAuth = await AuthHelper.RegisterAsync(a, name: "Ana");
        a.WithToken(aAuth.Token);
        var b = _factory.CreateClient();
        var bAuth = await AuthHelper.RegisterAsync(b, name: "Bia");
        b.WithToken(bAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/circles/invites/{token}/accept", new { }),
            b.PostAsJsonAsync($"/api/circles/invites/{token}/accept", new { }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{familia}", Json);
        Assert.Contains(detail.GetProperty("members").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == aAuth.UserId);
        Assert.Contains(detail.GetProperty("members").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == bAuth.UserId);
    }

    [Fact]
    public async Task Sequencia_DiaSeguidoSobeEMesmoDiaNaoDuplica()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var created = await client.PostAsJsonAsync("/api/shortcuts", new { label = "Cuidado", actionKind = "done" });
        var shortcut = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = shortcut.GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            await db.Users.Where(x => x.Id == auth.UserId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRoutineDate, yesterday)
                .SetProperty(x => x.RoutineStreakDays, 3));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/shortcuts/{id}/run", null)).StatusCode);
        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(4, home.GetProperty("routineStreakDays").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/shortcuts/{id}/run", null)).StatusCode);
        var again = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(4, again.GetProperty("routineStreakDays").GetInt32());
    }

    [Fact]
    public async Task PrivacidadeEExportarJson_MostramQuemVeOQue()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var privacy = await client.GetFromJsonAsync<JsonElement>("/api/privacy/me", Json);
        var items = privacy.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("what").GetString()).ToList();
        Assert.Contains(items, x => x!.Contains("Diário", StringComparison.Ordinal));
        Assert.Contains(items, x => x!.Contains("Eu", StringComparison.Ordinal));
        Assert.Contains(items, x => x!.Contains("Família", StringComparison.Ordinal));

        var export = await client.GetAsync("/api/export/me?format=json");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await export.Content.ReadAsStringAsync());
        Assert.Equal(auth.Email, doc.RootElement.GetProperty("profile").GetProperty("email").GetString());
    }

    [Fact]
    public async Task ListasGrandes_PaginamQuandoTakeVemEMantemTudoSemTake()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/memories", new { title = $"M{i}", occurredOn = "2020-01-0" + (i + 1) }))
                .EnsureSuccessStatusCode();
        }

        var page = await client.GetAsync("/api/memories?skip=0&take=2");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(page.Headers.TryGetValues("X-Total-Count", out var totals));
        Assert.Equal("3", totals.Single());
        var sliced = await page.Content.ReadFromJsonAsync<List<JsonElement>>(Json);
        Assert.Equal(2, sliced!.Count);

        var all = await client.GetFromJsonAsync<List<JsonElement>>("/api/memories", Json);
        Assert.Equal(3, all!.Count);
    }

    [Fact]
    public async Task Log_PermaneceEPaginacaoNaoApaga()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var circles = await client.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        var id = circles!.Single(x => x.GetProperty("name").GetString() == "Família").GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/circles/{id}/posts", new { body = "Recado" })).EnsureSuccessStatusCode();

        var full = await client.GetAsync($"/api/circles/{id}/log");
        var logs = await full.Content.ReadFromJsonAsync<List<JsonElement>>(Json);
        Assert.True(logs!.Count >= 2);
        Assert.Contains(logs, x => x.GetProperty("action").GetString() == "posted" && x.GetProperty("createdAt").GetDateTime() != default);
        var total = logs.Count;

        var page = await client.GetAsync($"/api/circles/{id}/log?skip=0&take=1");
        Assert.Equal(total.ToString(), page.Headers.GetValues("X-Total-Count").Single());
        var sliced = await page.Content.ReadFromJsonAsync<List<JsonElement>>(Json);
        Assert.Single(sliced!);

        var again = await client.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{id}/log", Json);
        Assert.Equal(total, again!.Count);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/circles/{id}/log")).StatusCode);
    }

    [Fact]
    public async Task Denuncia_AdminDoCirculoRecebeEmailEDecideComSobreposicao()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circles = await dono.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        var id = circles!.Single(x => x.GetProperty("name").GetString() == "Família").GetProperty("id").GetGuid();
        var post = await (await dono.PostAsJsonAsync($"/api/circles/{id}/posts", new { body = "Alvo" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        var reporter = _factory.CreateClient();
        reporter.WithToken((await AuthHelper.RegisterAsync(reporter, name: "Reporter")).Token);
        var reported = await reporter.PostAsJsonAsync("/api/reports", new { targetKind = "post", targetId = post.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.OK, reported.StatusCode);
        var reportId = (await reported.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.SentEmails.AnyAsync(x => x.To == donoAuth.Email && x.Subject.Contains("Denúncia")));
        }

        var list = await dono.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{id}/reports", Json);
        Assert.Contains(list!, x => x.GetProperty("id").GetGuid() == reportId);

        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/circles/{id}/reports/{reportId}/decide", new { decision = "hide" }),
            b.PostAsJsonAsync($"/api/circles/{id}/reports/{reportId}/decide", new { decision = "keep" }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.ContentReports.SingleAsync(x => x.Id == reportId);
            Assert.Equal("circle-resolved", stored.Status);
            Assert.True(stored.CircleDecision is "hide" or "keep");
        }
    }

    [Fact]
    public async Task Apelacao_BloqueadoRecorreSemPrazoESobrepostoFicaUm()
    {
        var pessoa = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(pessoa, name: "Alvo");
        pessoa.WithToken(auth.Token);
        var admin = await AdminClient();
        (await admin.PostAsJsonAsync("/api/admin/blocks", new { targetKind = "profile", targetId = auth.UserId, blocked = true }))
            .EnsureSuccessStatusCode();

        var a = _factory.CreateClient();
        var b = _factory.CreateClient();
        var body = new { email = auth.Email, password = "senha-segura-1", note = "Quero voltar a usar o app." };
        var responses = await Task.WhenAll(a.PostAsJsonAsync("/api/account/appeals", body), b.PostAsJsonAsync("/api/account/appeals", body));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));

        var appeals = await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/appeals", Json);
        var open = appeals!.Where(x => x.GetProperty("userId").GetGuid() == auth.UserId && x.GetProperty("status").GetString() == "open").ToList();
        Assert.Single(open);

        var appealId = open[0].GetProperty("id").GetGuid();
        var adminA = await AdminClient();
        var adminB = await AdminClient();
        var resolved = await Task.WhenAll(
            adminA.PostAsJsonAsync($"/api/admin/appeals/{appealId}/resolve", new { accept = true }),
            adminB.PostAsJsonAsync($"/api/admin/appeals/{appealId}/resolve", new { accept = true }));
        Assert.All(resolved, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await db.Users.SingleAsync(x => x.Id == auth.UserId)).BlockedAt);
        Assert.Equal(1, await db.AdminActions.CountAsync(x => x.TargetId == auth.UserId && x.Action == "appeal-accept"));
        Assert.Contains(await db.AdminActions.Where(x => x.TargetId == auth.UserId && x.Action == "appeal-accept").ToListAsync(), x => x.Detail.Contains("Sem prazo"));
    }

    [Fact]
    public async Task IdeiasDePresente_CompartilhaSoLeituraESobrepostoFicaUm()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circles = await dono.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        var familia = circles!.Single(x => x.GetProperty("name").GetString() == "Família").GetProperty("id").GetGuid();
        var reminder = await (await dono.PostAsJsonAsync("/api/reminders", new
        {
            title = "Aniversário",
            kind = "birthday",
            startAt = DateTime.UtcNow.Date.AddDays(20),
            personName = "Ana",
            giftIdeas = new[] { "Livro", "Planta" },
        })).Content.ReadFromJsonAsync<JsonElement>(Json);

        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/reminders/{reminder.GetProperty("id").GetGuid()}/share-ideas", new { circleId = familia }),
            b.PostAsJsonAsync($"/api/reminders/{reminder.GetProperty("id").GetGuid()}/share-ideas", new { circleId = familia }));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));

        var ideas = await dono.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{familia}/gift-ideas", Json);
        Assert.Single(ideas!);
        Assert.Equal("Aniversário", ideas[0].GetProperty("title").GetString());
        Assert.Equal(2, ideas[0].GetProperty("ideas").GetArrayLength());
    }

    [Fact]
    public async Task Lembrete_FotoSobeEBaixaComprimida()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var reminder = await (await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Pet",
            kind = "birthday",
            startAt = DateTime.UtcNow.Date.AddDays(8),
            personName = "Thor",
            giftIdeas = new[] { "Bola" },
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        using var form = PngForm();
        var upload = await client.PostAsync($"/api/reminders/{reminder.GetProperty("id").GetGuid()}/photo", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var body = await upload.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("hasPhoto").GetBoolean());
        var photo = await client.GetAsync($"/api/reminders/{reminder.GetProperty("id").GetGuid()}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Diario_PaginasTipoNotionEntramNoTextoCorrido()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var areas = await client.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.GetProperty("kind").GetString() == "diary").GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync($"/api/life-areas/{diario}/pages", new { title = "Livre", body = "Escrevi sem estrutura." });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var pages = await client.GetFromJsonAsync<List<JsonElement>>($"/api/life-areas/{diario}/pages", Json);
        Assert.Contains(pages!, x => x.GetProperty("title").GetString() == "Livre");

        var export = await client.GetAsync($"/api/life-areas/{diario}/export");
        var text = await export.Content.ReadAsStringAsync();
        Assert.Contains("Livre", text, StringComparison.Ordinal);
        Assert.Contains("Escrevi sem estrutura.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilaEmTabela_EnfileiraEProcessa()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var queued = await client.PostAsJsonAsync("/api/jobs", new { });
        Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        var body = await queued.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("done", body.GetProperty("status").GetString());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.BackgroundJobs.SingleAsync(x => x.Id == body.GetProperty("id").GetGuid());
        Assert.Equal("done", job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public async Task Telemetria_RetencaoIndefinidaEUploadSemTetoDe80Mb()
    {
        Assert.Equal("indefinite", TelemetryRetention.Policy);
        Assert.Equal(2L * 1024 * 1024 * 1024, FileRules.MaxIncomingBytes);
        Assert.Null(FileRules.ValidatePhoto("foto.jpg", "image/jpeg", 90L * 1024 * 1024));
        Assert.Equal("A foto está vazia.", FileRules.ValidatePhoto("foto.jpg", "image/jpeg", 0));
    }

    [Fact]
    public async Task Suporte_GravaOutboxSemPrazo()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/account/support", new { body = "Preciso de ajuda com o círculo." })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.SupportMessages.AnyAsync(x => x.UserId == auth.UserId));
        Assert.True(await db.SentEmails.AnyAsync(x => x.To == auth.Email && x.Body.Contains("Não há prazo")));
    }

    [Fact]
    public async Task Perdedor_PodeSeCandidatarNaRodadaSeguinte()
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
        var caio = _factory.CreateClient();
        var caioAuth = await AuthHelper.RegisterAsync(caio, name: "Caio");
        caio.WithToken(caioAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Casa", visibility = "public" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var circleId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        foreach (var (client, email) in new[] { (ana, anaAuth.Email), (bia, biaAuth.Email), (caio, caioAuth.Email) })
        {
            var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email, role = "viewer" }))
                .Content.ReadFromJsonAsync<JsonElement>(Json);
            (await client.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { }))
                .EnsureSuccessStatusCode();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await dono.DeleteAsync($"/api/circles/{circleId}/membership")).StatusCode);
        (await ana.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();
        (await bia.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = biaAuth.UserId })).EnsureSuccessStatusCode();
        (await caio.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = anaAuth.UserId })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(x => x.Id == circleId);
            Assert.Equal(anaAuth.UserId, stored.OwnerUserId);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/circles/{circleId}/membership")).StatusCode);
        var open = await bia.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("open", open.GetProperty("status").GetString());
        (await bia.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = biaAuth.UserId })).EnsureSuccessStatusCode();
        (await caio.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = biaAuth.UserId })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(x => x.Id == circleId);
            Assert.Equal(biaAuth.UserId, stored.OwnerUserId);
        }
    }

    [Fact]
    public async Task Plano_EmDiaOuNaoSemListaDeCobranca()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var plan = await client.GetFromJsonAsync<JsonElement>("/api/plan", Json);
        Assert.True(plan.TryGetProperty("inGoodStanding", out _));
        Assert.False(plan.TryGetProperty("charges", out _));
        Assert.Equal(0, plan.GetProperty("circleCount").GetInt32());
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

    private static MultipartFormDataContent PngForm()
    {
        using var image = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Opaque);
        image.Erase(new SKColor(30, 80, 120));
        using var encoded = SKImage.FromBitmap(image).Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Falha ao gerar PNG.");
        var file = new ByteArrayContent(encoded.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", "foto.png");
        return form;
    }
}

public sealed class GoogleEnabledFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Google:Enabled", "true");
    }
}

public sealed class GoogleEnabledTests : IClassFixture<GoogleEnabledFactory>
{
    private readonly GoogleEnabledFactory _factory;

    public GoogleEnabledTests(GoogleEnabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Google_LigadoSemClientId_Retorna503()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "qualquer" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
    }
}
