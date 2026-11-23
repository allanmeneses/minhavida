using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Circulos.Api;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class Rodada13Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada13Tests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Versionamento_SegueSemver100()
    {
        Assert.Equal("1.0.0", AppVersion.SemVer);
        var assemblyVersion = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.StartsWith("1.0.0", assemblyVersion);
    }

    [Fact]
    public async Task Armazenamento_AvisaEm90PorCento_EBloqueiaNoLimite()
    {
        Assert.False(PlanLimits.ShouldWarn((long)(PlanLimits.FreeStorageBytes * 0.89), PlanLimits.FreeStorageBytes));
        Assert.True(PlanLimits.ShouldWarn((long)(PlanLimits.FreeStorageBytes * 0.90), PlanLimits.FreeStorageBytes));

        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(x => x.Id == auth.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StorageUsedBytes, (long)(PlanLimits.FreeStorageBytes * 0.90)));
        }

        var warned = await client.GetFromJsonAsync<JsonElement>("/api/plan", Json);
        Assert.True(warned.GetProperty("storageWarning").GetBoolean());
        Assert.Equal(PlanLimits.FreeStorageBytes, warned.GetProperty("storageLimitBytes").GetInt64());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(x => x.Id == auth.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StorageUsedBytes, PlanLimits.FreeStorageBytes));
        }

        using var form = TextFile("extra.txt", "não cabe");
        var blocked = await client.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }

    [Fact]
    public async Task CatalogoPago_SoCirculosAtalhosEStorage()
    {
        var client = _factory.CreateClient();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/plans", Json);
        var paid = catalog.GetProperty("paidMonthly");
        Assert.True(
            !paid.TryGetProperty("circleLimit", out var circleLimit) || circleLimit.ValueKind is JsonValueKind.Null);

        Assert.Equal(PlanLimits.PaidStorageBytes, paid.GetProperty("storageLimitBytes").GetInt64());
        Assert.Equal(PlanLimits.PaidShortcutLimit, paid.GetProperty("shortcutLimit").GetInt32());
        var summary = paid.GetProperty("summary").GetString() ?? string.Empty;
        Assert.Contains("Círculos ilimitados", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("PDF", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100 membros", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prioridade", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CirculoTravado_NaoAceitaPostNemSobrepostoComATrava()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var editor = _factory.CreateClient();
        var editorAuth = await AuthHelper.RegisterAsync(editor, name: "Editor");
        editor.WithToken(editorAuth.Token);
        var segundo = _factory.CreateClient();
        var segundoAuth = await AuthHelper.RegisterAsync(segundo, name: "Segundo");
        segundo.WithToken(segundoAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Travado", visibility = "public" });
        var circleId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await InviteAsync(dono, editor, editorAuth.Email, circleId, "editor");
        await InviteAsync(dono, segundo, segundoAuth.Email, circleId, "viewer");
        Assert.Equal(HttpStatusCode.NoContent, (await dono.DeleteAsync($"/api/circles/{circleId}/membership")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(item => item.Id == circleId);
            stored.Status = CircleStatus.LockedNoOwner;
            stored.OwnershipNextCheckAt = DateTime.UtcNow.AddDays(40);
            await db.SaveChangesAsync();
        }

        var outroEditor = _factory.CreateClient();
        outroEditor.WithToken(editorAuth.Token);
        var first = editor.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "Durante a trava" });
        var overlap = outroEditor.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "Também durante a trava" });
        await Task.WhenAll(first, overlap);
        var overlapResponse = await overlap;
        var firstResponse = await first;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.Include(item => item.Posts).SingleAsync(item => item.Id == circleId);
            Assert.Equal(CircleStatus.LockedNoOwner, stored.Status);
            Assert.DoesNotContain(stored.Posts, post => post.Body.Contains("Durante a trava"));
            Assert.Equal(HttpStatusCode.BadRequest, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, overlapResponse.StatusCode);
        }

        var after = await editor.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "Depois da trava" });
        Assert.Equal(HttpStatusCode.BadRequest, after.StatusCode);
        var body = await after.Content.ReadAsStringAsync();
        Assert.Contains("travado", body, StringComparison.OrdinalIgnoreCase);

        var reminder = await editor.PostAsJsonAsync("/api/reminders", new
        {
            title = "Não no travado",
            startAt = DateTime.UtcNow.AddHours(2),
            circleId,
            recurrence = new { frequency = "none", interval = 1 },
        });
        Assert.Equal(HttpStatusCode.BadRequest, reminder.StatusCode);

        var memory = await editor.PostAsJsonAsync("/api/memories", new { title = "Não no travado", circleId });
        Assert.Equal(HttpStatusCode.BadRequest, memory.StatusCode);
    }

    [Fact]
    public async Task Post_EmBranco_Devolve400()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var created = await client.PostAsJsonAsync("/api/circles", new { name = "Vazio", visibility = "private" });
        var circleId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var blank = await client.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Contains("vazia", await blank.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatusHtml_CitaSondaHealth()
    {
        var client = _factory.CreateClient();
        var html = await (await client.GetAsync("/status")).Content.ReadAsStringAsync();
        Assert.Contains("GET /health", html, StringComparison.Ordinal);
    }

    private static async Task InviteAsync(HttpClient owner, HttpClient member, string email, Guid circleId, string role)
    {
        var invite = await (await owner.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email, role }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await member.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { }))
            .EnsureSuccessStatusCode();
    }

    private static MultipartFormDataContent TextFile(string name, string content)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", name);
        return form;
    }
}
