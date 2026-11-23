using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class RevealTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public RevealTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Revelacao_DevolveResumoComMensagensEFotos_HomenageadoGuardaNoPerfil()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var honrado = _factory.CreateClient();
        var honradoAuth = await AuthHelper.RegisterAsync(honrado, name: "Aniversariante");
        honrado.WithToken(honradoAuth.Token);

        var circle = await (await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Festa",
            description = "Não contar",
            isSurprise = true,
            surpriseSubjectEmail = honradoAuth.Email,
        })).Content.ReadFromJsonAsync<CircleDto>(Json);

        (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/posts", new { body = "Feliz aniversário" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await honrado.GetAsync($"/api/circles/{circle.Id}/reveal-summary")).StatusCode);

        var reveal = await dono.PostAsync($"/api/circles/{circle.Id}/reveal", null);
        Assert.Equal(HttpStatusCode.OK, reveal.StatusCode);
        var summary = await reveal.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Festa", summary.GetProperty("name").GetString());
        Assert.Contains(summary.GetProperty("messages").EnumerateArray(), x => x.GetProperty("body").GetString() == "Feliz aniversário");

        var honradoSummary = await honrado.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}/reveal-summary", Json);
        Assert.Equal("Festa", honradoSummary.GetProperty("name").GetString());
        var mine = await honrado.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json);
        Assert.Contains(mine!, x => x.GetProperty("id").GetGuid() == circle.Id);
        Assert.Equal(HttpStatusCode.OK, (await honrado.GetAsync($"/api/circles/{circle.Id}")).StatusCode);

        var kept = await honrado.PostAsync($"/api/circles/{circle.Id}/keep-memory", null);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        var memory = await kept.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("Recordação", memory.GetProperty("title").GetString());
        Assert.Contains("Feliz aniversário", memory.GetProperty("body").GetString());

        var edited = await honrado.PutAsJsonAsync($"/api/memories/{memory.GetProperty("id").GetGuid()}", new
        {
            title = "Minha festa",
            body = "Editei em silêncio",
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var list = await honrado.GetFromJsonAsync<JsonElement>("/api/memories", Json);
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("title").GetString() == "Minha festa");

        var log = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}/log", Json);
        Assert.DoesNotContain(log.EnumerateArray(), x => x.GetProperty("action").GetString() == "memory-edited");
    }

    [Fact]
    public async Task RevelacaoSobreposta_OsDoisPassamEFicaRevelado()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Dupla", isSurprise = true })).Content.ReadFromJsonAsync<CircleDto>(Json);
        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsync($"/api/circles/{circle!.Id}/reveal", null),
            b.PostAsync($"/api/circles/{circle.Id}/reveal", null));
        Assert.All(responses, x => Assert.True((int)x.StatusCode is 200 or 404));
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.OK);
        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        Assert.True(detail.GetProperty("revealed").GetBoolean());
    }

    private sealed record CircleDto(Guid Id);
}
