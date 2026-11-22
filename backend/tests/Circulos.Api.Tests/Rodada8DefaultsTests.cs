using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Domain;

namespace Circulos.Api.Tests;

public sealed class Rodada8DefaultsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada8DefaultsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Atalho_TetoDoGratisERecuperaExcluidoComPaleta()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        Guid? firstId = null;
        for (var i = 0; i < PlanLimits.FreeShortcutLimit; i++)
        {
            var created = await client.PostAsJsonAsync("/api/shortcuts", new { label = $"A{i}", actionKind = i == 0 ? "start" : "done", sortOrder = i });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var body = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
            firstId ??= body.GetProperty("id").GetGuid();
            if (i == 0)
            {
                Assert.Equal("start", body.GetProperty("actionKind").GetString());
                Assert.Equal(ShortcutPalette.Start, body.GetProperty("color").GetString());
            }
        }

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/shortcuts", new { label = "Sexto" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/shortcuts/{firstId}")).StatusCode);
        var listed = await client.GetFromJsonAsync<List<JsonElement>>("/api/shortcuts", Json);
        Assert.DoesNotContain(listed!, item => item.GetProperty("id").GetGuid() == firstId);
        var withDeleted = await client.GetFromJsonAsync<List<JsonElement>>("/api/shortcuts?includeDeleted=true", Json);
        Assert.Contains(withDeleted!, item => item.GetProperty("id").GetGuid() == firstId && item.GetProperty("isDeleted").GetBoolean());

        var restored = await client.PostAsync($"/api/shortcuts/{firstId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/shortcuts", new { label = "Ainda cheio" })).StatusCode);
    }

    [Fact]
    public async Task Lista_UltimaSincronizacaoVenceMesmoSobreposta()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var list = await (await client.PostAsJsonAsync("/api/lists", new { name = "Feira" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var listId = list.GetProperty("id").GetGuid();
        var item = await (await client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Arroz" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var itemId = item.GetProperty("id").GetGuid();
        var known = item.GetProperty("updatedAt").GetDateTime();

        var a = _factory.CreateClient();
        a.WithToken(client.DefaultRequestHeaders.Authorization!.Parameter!);
        var b = _factory.CreateClient();
        b.WithToken(client.DefaultRequestHeaders.Authorization!.Parameter!);
        var first = a.PutAsJsonAsync($"/api/lists/{listId}/items/{itemId}", new { text = "Arroz A", knownUpdatedAt = known });
        var second = b.PutAsJsonAsync($"/api/lists/{listId}/items/{itemId}", new { text = "Arroz B", knownUpdatedAt = known });
        await Task.WhenAll(first, second);
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second).StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/lists/{listId}", Json);
        var text = detail.GetProperty("items")[0].GetProperty("text").GetString();
        Assert.True(text is "Arroz A" or "Arroz B");
        var stale = await client.PutAsJsonAsync(
            $"/api/lists/{listId}/items/{itemId}",
            new { text = "Arroz C", knownUpdatedAt = known });
        var updated = await stale.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Arroz C", updated.GetProperty("text").GetString());
        Assert.True(updated.GetProperty("lastSyncWins").GetBoolean());
    }

    [Fact]
    public async Task Log_FiltraPorAcao()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var circle = await (await client.PostAsJsonAsync("/api/circles", new { name = "Log" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = circle.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/circles/{id}/posts", new { body = "Oi" })).EnsureSuccessStatusCode();
        var filtered = await client.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{id}/log?action=posted", Json);
        Assert.All(filtered!, item => Assert.Equal("posted", item.GetProperty("action").GetString()));
        var created = await client.GetFromJsonAsync<List<JsonElement>>($"/api/circles/{id}/log?action=created", Json);
        Assert.All(created!, item => Assert.Equal("created", item.GetProperty("action").GetString()));
    }
}
