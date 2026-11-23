using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class TemplateTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public TemplateTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Catalogo_TemModelosPtBrAnonimosECopiaEditavel()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/templates", Json);
        Assert.NotNull(list);
        Assert.Contains(list, x => x.GetProperty("name").GetString() == "Compras da semana");
        Assert.Contains(list, x => x.GetProperty("name").GetString() == "Tomei o remédio");
        Assert.All(list, x => Assert.False(x.TryGetProperty("creatorUserId", out _)));

        var copy = await client.PostAsync($"/api/templates/{TemplateCatalog.ComprasDaSemanaId}/copy", null);
        Assert.Equal(HttpStatusCode.Created, copy.StatusCode);
        var created = await copy.Content.ReadFromJsonAsync<JsonElement>(Json);
        var listId = created.GetProperty("id").GetGuid();
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/lists/{listId}", Json);
        Assert.Equal("Compras da semana", detail.GetProperty("name").GetString());
        Assert.Contains(detail.GetProperty("items").EnumerateArray(), x => x.GetProperty("text").GetString() == "Leite");

        var renamed = await client.PutAsJsonAsync($"/api/lists/{listId}/items/{detail.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid()}", new { text = "Leite desnatado", quantity = "1 L" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
    }

    [Fact]
    public async Task CopiaLembrete_CriaRecorrenciaEditavel()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var copy = await client.PostAsync($"/api/templates/{TemplateCatalog.TomeiORemedioId}/copy", null);
        Assert.Equal(HttpStatusCode.Created, copy.StatusCode);
        var created = await copy.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("reminder", created.GetProperty("kind").GetString());
        var reminders = await client.GetFromJsonAsync<List<JsonElement>>("/api/reminders", Json);
        Assert.Contains(reminders!, x => x.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task CopiaSobreposta_ContaUsoDuasVezes()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        (await client.GetAsync("/api/templates")).EnsureSuccessStatusCode();
        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(
            a.PostAsync($"/api/templates/{TemplateCatalog.CorridaSemanalId}/copy", null),
            b.PostAsync($"/api/templates/{TemplateCatalog.CorridaSemanalId}/copy", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Created, x.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await db.RoutineTemplates.SingleAsync(x => x.Id == TemplateCatalog.CorridaSemanalId);
        Assert.Equal(2, template.UseCount);
    }

    [Fact]
    public async Task PessoaCriaModeloDeLista_SemNomeDoCriador()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var list = await (await client.PostAsJsonAsync("/api/lists", new { name = "Minha feira", kind = "shopping" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        (await client.PostAsJsonAsync($"/api/lists/{list.GetProperty("id").GetGuid()}/items", new { text = "Tomate" })).EnsureSuccessStatusCode();
        var created = await client.PostAsJsonAsync("/api/templates", new
        {
            kind = "list",
            name = "Feira da casa",
            description = "O que eu costumo comprar",
            listId = list.GetProperty("id").GetGuid(),
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(body.TryGetProperty("creatorUserId", out _));
        Assert.Equal("Feira da casa", body.GetProperty("name").GetString());
    }
}
