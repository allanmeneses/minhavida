using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class SharedListTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public SharedListTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Solo_CriaListaAdicionaItemMarcaReordenaEExclui()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        var created = await client.PostAsJsonAsync("/api/lists", new { name = "Feira", kind = "shopping" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var list = await created.Content.ReadFromJsonAsync<ListDto>(Json);

        var arroz = await client.PostAsJsonAsync($"/api/lists/{list!.Id}/items", new { text = "Arroz", quantity = "2 kg", note = "tipo 1" });
        var feijao = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { text = "Feijao" });
        var a = await arroz.Content.ReadFromJsonAsync<ItemDto>(Json);
        var b = await feijao.Content.ReadFromJsonAsync<ItemDto>(Json);
        Assert.Equal(auth.UserId, a!.AddedByUserId);
        Assert.Equal("Maria Silva", a.AddedByName);

        var done = await client.PostAsync($"/api/lists/{list.Id}/items/{a.Id}/done", null);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var marked = await done.Content.ReadFromJsonAsync<ItemDto>(Json);
        Assert.True(marked!.IsDone);
        Assert.Equal(auth.UserId, marked.DoneByUserId);

        var reorder = await client.PutAsJsonAsync($"/api/lists/{list.Id}/items/order", new { ids = new[] { b!.Id, a.Id } });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/lists/{list.Id}", Json);
        var texts = detail.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("text").GetString()).ToArray();
        Assert.Equal(new[] { "Feijao", "Arroz" }, texts);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/lists/{list.Id}/items/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/lists/{list.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/lists/{list.Id}")).StatusCode);
    }

    [Fact]
    public async Task Circulo_DoisMembrosVeemAMesmaListaMesmoComEscritaSobreposta()
    {
        var (dono, membro, listId) = await CircleListAsync("add");
        var first = dono.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Leite" });
        var second = membro.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Pao" });
        await Task.WhenAll(first, second);
        Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await second).StatusCode);

        var seenByMember = await membro.Client.GetFromJsonAsync<JsonElement>($"/api/lists/{listId}", Json);
        var texts = seenByMember.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("text").GetString()).ToArray();
        Assert.Contains("Leite", texts);
        Assert.Contains("Pao", texts);
    }

    [Fact]
    public async Task PermissaoView_MembroSoVe_403NoResto()
    {
        var (dono, membro, listId) = await CircleListAsync("view");
        var item = await (await dono.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Arroz" })).Content.ReadFromJsonAsync<ItemDto>(Json);

        var seen = await membro.Client.GetFromJsonAsync<JsonElement>($"/api/lists/{listId}", Json);
        Assert.Equal("view", seen.GetProperty("myAccess").GetString());
        Assert.False(seen.GetProperty("canAdd").GetBoolean());
        Assert.Equal("Arroz", seen.GetProperty("items")[0].GetProperty("text").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Feijao" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PostAsync($"/api/lists/{listId}/items/{item!.Id}/done", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PutAsJsonAsync($"/api/lists/{listId}/items/{item.Id}", new { text = "Arroz integral" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.DeleteAsync($"/api/lists/{listId}/items/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PutAsJsonAsync($"/api/lists/{listId}/access", new { memberAccess = "manage" })).StatusCode);
    }

    [Fact]
    public async Task PermissaoAdd_MembroAdiciona_403NoDone()
    {
        var (dono, membro, listId) = await CircleListAsync("add");
        var created = await membro.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Leite" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await created.Content.ReadFromJsonAsync<ItemDto>(Json);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PostAsync($"/api/lists/{listId}/items/{item!.Id}/done", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PutAsJsonAsync($"/api/lists/{listId}/items/{item.Id}", new { text = "Leite desnatado" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await dono.Client.GetAsync($"/api/lists/{listId}")).StatusCode);
    }

    [Fact]
    public async Task PermissaoComplete_MembroMarca_403NoEditar()
    {
        var (dono, membro, listId) = await CircleListAsync("complete");
        var item = await (await dono.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Sabao" })).Content.ReadFromJsonAsync<ItemDto>(Json);
        Assert.Equal(HttpStatusCode.OK, (await membro.Client.PostAsync($"/api/lists/{listId}/items/{item!.Id}/done", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PutAsJsonAsync($"/api/lists/{listId}/items/{item.Id}", new { text = "Sabao em po" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.DeleteAsync($"/api/lists/{listId}/items/{item.Id}")).StatusCode);
    }

    [Fact]
    public async Task PermissaoManage_MembroEditaEExclui()
    {
        var (_, membro, listId) = await CircleListAsync("manage");
        var item = await (await membro.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Tinta" })).Content.ReadFromJsonAsync<ItemDto>(Json);
        var edited = await membro.Client.PutAsJsonAsync($"/api/lists/{listId}/items/{item!.Id}", new { text = "Tinta branca", quantity = "1 L" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await membro.Client.PostAsync($"/api/lists/{listId}/items/{item.Id}/done", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await membro.Client.DeleteAsync($"/api/lists/{listId}/items/{item.Id}")).StatusCode);
    }

    [Fact]
    public async Task PermissaoAlteradaDepois_PassaAValer()
    {
        var (dono, membro, listId) = await CircleListAsync("view");
        var item = await (await dono.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Ovo" })).Content.ReadFromJsonAsync<ItemDto>(Json);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.PostAsync($"/api/lists/{listId}/items/{item!.Id}/done", null)).StatusCode);

        var changed = await dono.Client.PutAsJsonAsync($"/api/lists/{listId}/access", new { memberAccess = "complete" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await membro.Client.PostAsync($"/api/lists/{listId}/items/{item.Id}/done", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await membro.Client.DeleteAsync($"/api/lists/{listId}/items/{item.Id}")).StatusCode);
    }

    [Fact]
    public async Task PermissaoPorMembro_SobrescreveOPadrao()
    {
        var (dono, membro, listId) = await CircleListAsync("view");
        var outro = _factory.CreateClient();
        var outroAuth = await AuthHelper.RegisterAsync(outro, name: "Outro");
        outro.WithToken(outroAuth.Token);
        var invite = await (await dono.Client.PostAsJsonAsync($"/api/circles/{dono.CircleId}/invites", new { email = outroAuth.Email, role = "editor" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await outro.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();

        var grant = await dono.Client.PutAsJsonAsync($"/api/lists/{listId}/grants", new { userId = membro.Auth.UserId, access = "manage" });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        var item = await (await membro.Client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Queijo" })).Content.ReadFromJsonAsync<ItemDto>(Json);
        Assert.Equal(HttpStatusCode.OK, (await membro.Client.PutAsJsonAsync($"/api/lists/{listId}/items/{item!.Id}", new { text = "Queijo minas" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outro.PostAsJsonAsync($"/api/lists/{listId}/items", new { text = "Presunto" })).StatusCode);
    }

    [Fact]
    public async Task Surpresa_HomenageadoNaoVeListaDeDesejos()
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
            isSurprise = true,
            surpriseSubjectEmail = honradoAuth.Email,
        })).Content.ReadFromJsonAsync<CircleDto>(Json);

        var created = await dono.PostAsJsonAsync("/api/lists", new { name = "Presentes", kind = "wishes", circleId = circle!.Id });
        var list = await created.Content.ReadFromJsonAsync<ListDto>(Json);
        var ownerListed = await dono.GetFromJsonAsync<JsonElement>($"/api/lists?circleId={circle.Id}", Json);
        Assert.Contains(ownerListed.EnumerateArray(), x => x.GetProperty("name").GetString() == "Presentes");
        Assert.Equal(HttpStatusCode.NotFound, (await honrado.GetAsync($"/api/lists/{list!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await honrado.GetAsync($"/api/lists?circleId={circle.Id}")).StatusCode);

        (await dono.PostAsync($"/api/circles/{circle.Id}/reveal", null)).EnsureSuccessStatusCode();
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle.Id}/invites", new { email = honradoAuth.Email, role = "editor" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await honrado.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();
        var visible = await honrado.GetAsync($"/api/lists/{list.Id}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        var listedAfterReveal = await honrado.GetFromJsonAsync<JsonElement>($"/api/lists?circleId={circle.Id}", Json);
        Assert.Contains(listedAfterReveal.EnumerateArray(), x => x.GetProperty("name").GetString() == "Presentes");
    }

    [Fact]
    public async Task Viewer_NaoCriaListaDoCirculo()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var viewer = _factory.CreateClient();
        var viewerAuth = await AuthHelper.RegisterAsync(viewer, name: "Visitante");
        viewer.WithToken(viewerAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Clube" })).Content.ReadFromJsonAsync<CircleDto>(Json);
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = viewerAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await viewer.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/lists", new { name = "Outra", kind = "other", circleId = circle.Id })).StatusCode);
    }

    private async Task<(Party Dono, Party Membro, Guid ListId)> CircleListAsync(string memberAccess)
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Casa" })).Content.ReadFromJsonAsync<CircleDto>(Json);
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = membroAuth.Email, role = "editor" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await membro.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();
        var created = await dono.PostAsJsonAsync("/api/lists", new
        {
            name = "Compras da casa",
            kind = "shopping",
            circleId = circle.Id,
            memberAccess,
        });
        created.EnsureSuccessStatusCode();
        var list = await created.Content.ReadFromJsonAsync<ListDto>(Json);
        var listed = await dono.GetFromJsonAsync<JsonElement>($"/api/lists?circleId={circle.Id}", Json);
        Assert.Contains(listed.EnumerateArray(), x => x.GetProperty("name").GetString() == "Compras da casa");
        return (
            new Party(dono, donoAuth, circle.Id),
            new Party(membro, membroAuth, circle.Id),
            list!.Id);
    }

    private sealed record Party(HttpClient Client, AuthHelper.AuthResult Auth, Guid CircleId);
    private sealed record ListDto(Guid Id, string Name, string Kind);
    private sealed record ItemDto(Guid Id, string Text, Guid? AddedByUserId, string? AddedByName, Guid? DoneByUserId, bool IsDone);
    private sealed record CircleDto(Guid Id);
    private sealed record InviteDto(string Token);
}
