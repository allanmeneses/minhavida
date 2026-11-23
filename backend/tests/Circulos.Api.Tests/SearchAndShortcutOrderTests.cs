using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class SearchAndShortcutOrderTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public SearchAndShortcutOrderTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Busca_UnificadaComHistoricoSincronizadoETermosFrequentes()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var areas = await client.GetFromJsonAsync<List<JsonArea>>("/api/life-areas", Json);
        var diary = areas!.Single(x => x.Kind == "diary");
        await client.PostAsJsonAsync($"/api/life-areas/{diary.Id}/entries", new { title = "Texto livre da medicação", body = "nota" });
        await client.PostAsJsonAsync("/api/circles", new { name = "Clube medicação", visibility = "public", discoverable = true });
        await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Aniversário medicação",
            kind = "birthday",
            startAt = DateTime.UtcNow.AddDays(20),
            personName = "Ana",
            recurrence = new { frequency = "yearly", interval = 1 },
        });

        for (var i = 0; i < 2; i++)
        {
            var first = await client.GetFromJsonAsync<JsonElement>("/api/search?q=medicação", Json);
            Assert.Contains(first.GetProperty("hits").EnumerateArray(), x => x.GetProperty("type").GetString() == "diary");
            Assert.Contains(first.GetProperty("hits").EnumerateArray(), x => x.GetProperty("type").GetString() == "circle");
            Assert.Contains(first.GetProperty("hits").EnumerateArray(), x => x.GetProperty("type").GetString() == "birthday");
        }

        await client.GetAsync("/api/search?q=clube");
        var history = await client.GetFromJsonAsync<JsonElement>("/api/search/history", Json);
        Assert.Equal("medicação", history.GetProperty("frequent").EnumerateArray().First().GetString());
        Assert.Contains(history.GetProperty("recent").EnumerateArray().Select(x => x.GetString()), x => x == "clube");

        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/search?q=medicação&type=circle", Json);
        Assert.All(filtered.GetProperty("hits").EnumerateArray(), x => Assert.Equal("circle", x.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Atalho_ReordenaNaTela()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var a = await (await client.PostAsJsonAsync("/api/shortcuts", new { label = "Primeiro", sortOrder = 0 })).Content.ReadFromJsonAsync<ShortcutDto>(Json);
        var b = await (await client.PostAsJsonAsync("/api/shortcuts", new { label = "Segundo", sortOrder = 1 })).Content.ReadFromJsonAsync<ShortcutDto>(Json);
        var reorder = await client.PutAsJsonAsync("/api/shortcuts/order", new { ids = new[] { b!.Id, a!.Id } });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);
        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        var labels = home.GetProperty("shortcuts").EnumerateArray().Select(x => x.GetProperty("label").GetString() ?? "").ToArray();
        Assert.Equal(new[] { "Segundo", "Primeiro" }, labels);
    }

    private sealed record JsonArea(Guid Id, string Kind);
    private sealed record ShortcutDto(Guid Id, string Label);
}
