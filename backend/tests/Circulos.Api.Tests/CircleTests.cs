using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class CircleTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public CircleTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dono_CriaConviteEOutroAceita()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);

        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Familia", visibility = "private" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);

        var invite = await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = membroAuth.Email, role = "editor" });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var token = (await invite.Content.ReadFromJsonAsync<InviteDto>(Json))!.Token;

        var accept = await membro.PostAsJsonAsync($"/api/circles/invites/{token}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var detail = await membro.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        Assert.Equal("Familia", detail.GetProperty("name").GetString());
        Assert.Contains(detail.GetProperty("members").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == membroAuth.UserId);
    }

    [Fact]
    public async Task Publico_VisitanteVeSoNomeEDescricao()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono);
        dono.WithToken(donoAuth.Token);
        var created = await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Clube",
            description = "Corrida de rua",
            visibility = "public",
            discoverable = true,
        });
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);

        await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/posts", new { body = "Segredo do grupo" });

        var visitante = _factory.CreateClient();
        var visitanteAuth = await AuthHelper.RegisterAsync(visitante, name: "Visitante");
        visitante.WithToken(visitanteAuth.Token);
        var preview = await visitante.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        Assert.Equal("Clube", preview.GetProperty("name").GetString());
        Assert.Equal("Corrida de rua", preview.GetProperty("description").GetString());
        Assert.Empty(preview.GetProperty("posts").EnumerateArray());
        Assert.Empty(preview.GetProperty("log").EnumerateArray());
    }

    [Fact]
    public async Task Surpresa_AniversarianteNaoVeOCirculo()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var honrado = _factory.CreateClient();
        var honradoAuth = await AuthHelper.RegisterAsync(honrado, name: "Aniversariante");
        honrado.WithToken(honradoAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Festa surpresa",
            description = "Nao contar",
            isSurprise = true,
            surpriseSubjectEmail = honradoAuth.Email,
        });
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);

        var hidden = await honrado.GetAsync($"/api/circles/{circle!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var list = await honrado.GetFromJsonAsync<List<SummaryDto>>("/api/circles", Json);
        Assert.DoesNotContain(list!, x => x.Id == circle.Id);
    }

    [Fact]
    public async Task PedidoDeEntrada_DonoAceitaEVisitanteEntra()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var visitante = _factory.CreateClient();
        var visitanteAuth = await AuthHelper.RegisterAsync(visitante, name: "Visitante");
        visitante.WithToken(visitanteAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Grupo fechado",
            description = "So com pedido",
            visibility = "private",
            discoverable = true,
        });
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);

        var discover = await visitante.GetFromJsonAsync<List<JsonElement>>("/api/circles/discover?q=fechado", Json);
        Assert.Contains(discover!, x => x.GetProperty("id").GetGuid() == circle!.Id);

        var preview = await visitante.GetFromJsonAsync<JsonElement>($"/api/circles/{circle!.Id}", Json);
        Assert.Equal("Grupo fechado", preview.GetProperty("name").GetString());
        Assert.Empty(preview.GetProperty("posts").EnumerateArray());
        Assert.Empty(preview.GetProperty("members").EnumerateArray());

        var ask = await visitante.PostAsync($"/api/circles/{circle.Id}/join", null);
        Assert.Equal(HttpStatusCode.Accepted, ask.StatusCode);

        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        var request = detail.GetProperty("joinRequests").EnumerateArray().Single();
        Assert.Equal(visitanteAuth.UserId, request.GetProperty("userId").GetGuid());

        var accept = await dono.PostAsync(
            $"/api/circles/{circle.Id}/join-requests/{request.GetProperty("id").GetGuid()}/accept",
            null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var inside = await visitante.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        Assert.Contains(inside.GetProperty("members").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == visitanteAuth.UserId);
    }

    [Fact]
    public async Task Discover_NaoListaCirculoQueNaoQuerSerEncontrado()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono)).Token);
        var created = await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Invisivel",
            visibility = "public",
            discoverable = false,
        });
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);

        var visitante = _factory.CreateClient();
        visitante.WithToken((await AuthHelper.RegisterAsync(visitante, name: "Outro")).Token);
        var discover = await visitante.GetFromJsonAsync<List<JsonElement>>("/api/circles/discover", Json);
        Assert.DoesNotContain(discover!, x => x.GetProperty("id").GetGuid() == circle!.Id);
    }

    [Fact]
    public async Task EntradaApagada_DeixaRastroNoLog()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var created = await client.PostAsJsonAsync("/api/circles", new { name = "Grupo" });
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);
        var posted = await client.PostAsJsonAsync($"/api/circles/{circle!.Id}/posts", new { body = "Nota" });
        var post = await posted.Content.ReadFromJsonAsync<PostDto>(Json);
        var delete = await client.DeleteAsync($"/api/circles/{circle.Id}/posts/{post!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var log = await client.GetFromJsonAsync<List<LogDto>>($"/api/circles/{circle.Id}/log", Json);
        Assert.Contains(log!, x => x.Action == "deleted" && x.ActorUserId == auth.UserId && x.CreatedAt != default);
        Assert.DoesNotContain(log!, x => x.Detail.Contains("Nota", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sair_NaoDeixaRastroDeParticipacao()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Grupo" });
        var circle = await created.Content.ReadFromJsonAsync<SummaryDto>(Json);
        var invite = await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = membroAuth.Email, role = "viewer" });
        var token = (await invite.Content.ReadFromJsonAsync<InviteDto>(Json))!.Token;
        (await membro.PostAsJsonAsync($"/api/circles/invites/{token}/accept", new { })).EnsureSuccessStatusCode();

        var leave = await membro.DeleteAsync($"/api/circles/{circle.Id}/membership");
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{circle.Id}", Json);
        Assert.DoesNotContain(detail.GetProperty("members").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == membroAuth.UserId);
        Assert.DoesNotContain(detail.GetProperty("log").EnumerateArray(), x => x.GetProperty("action").GetString() == "left");
    }

    [Fact]
    public async Task PlanoGratis_TerceiroCirculoPassaEQuartoFalhaMesmoSobreposto()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        for (var i = 0; i < 2; i++)
        {
            (await client.PostAsJsonAsync("/api/circles", new { name = $"C{i}" })).EnsureSuccessStatusCode();
        }

        var outro = _factory.CreateClient();
        outro.WithToken(auth.Token);
        var t1 = client.PostAsJsonAsync("/api/circles", new { name = "C2" });
        var t2 = outro.PostAsJsonAsync("/api/circles", new { name = "C3" });
        var responses = await Task.WhenAll(t1, t2);
        var codes = responses.Select(x => x.StatusCode).ToArray();
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.BadRequest, codes);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Circles.CountAsync(x => x.OwnerUserId == auth.UserId);
        Assert.Equal(3, count);
        var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
        Assert.Equal(3, user.OwnedCircleCount);
    }

    private sealed record SummaryDto(Guid Id, string? Name);
    private sealed record InviteDto(string Token);
    private sealed record PostDto(Guid Id);
    private sealed record LogDto(Guid Id, Guid? ActorUserId, string Action, string Detail, DateTime CreatedAt);
}
