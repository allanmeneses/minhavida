using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class LivingProfileTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public LivingProfileTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DonoCriaPetEGrupo_MembroVe_NaoEdita()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var membro = _factory.CreateClient();
        var membroAuth = await AuthHelper.RegisterAsync(membro, name: "Membro");
        membro.WithToken(membroAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Casa" })).Content.ReadFromJsonAsync<CircleDto>(Json);
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = membroAuth.Email, role = "editor" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await membro.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();

        var pet = await dono.PostAsJsonAsync("/api/living-profiles", new
        {
            name = "Luna",
            kind = "pet",
            context = "Nossa cachorra",
            circleId = circle.Id,
        });
        Assert.Equal(HttpStatusCode.Created, pet.StatusCode);
        var petBody = await pet.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("pet", petBody.GetProperty("kind").GetString());

        var group = await dono.PostAsJsonAsync("/api/living-profiles", new
        {
            name = "Turma da corrida",
            kind = "group",
            context = "Treino de rua",
        });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);

        var shared = await membro.GetFromJsonAsync<List<JsonElement>>($"/api/living-profiles?circleId={circle.Id}", Json);
        Assert.Contains(shared!, x => x.GetProperty("name").GetString() == "Luna");
        Assert.Contains(shared!, x => x.GetProperty("canEdit").GetBoolean() == false);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await membro.PutAsJsonAsync($"/api/living-profiles/{petBody.GetProperty("id").GetGuid()}", new { name = "Luna editada", kind = "pet" })).StatusCode);

        var mine = await dono.GetFromJsonAsync<List<JsonElement>>("/api/living-profiles", Json);
        Assert.Contains(mine!, x => x.GetProperty("name").GetString() == "Turma da corrida");
        Assert.Contains(mine!, x => x.GetProperty("kind").GetString() == "pet");
    }

    [Fact]
    public async Task Viewer_NaoLigaPerfilNoCirculo()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var viewer = _factory.CreateClient();
        var viewerAuth = await AuthHelper.RegisterAsync(viewer, name: "Viewer");
        viewer.WithToken(viewerAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Clube" })).Content.ReadFromJsonAsync<CircleDto>(Json);
        var invite = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = viewerAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await viewer.PostAsJsonAsync($"/api/circles/invites/{invite!.Token}/accept", new { })).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await viewer.PostAsJsonAsync("/api/living-profiles", new { name = "Gato", kind = "pet", circleId = circle.Id })).StatusCode);
    }

    private sealed record CircleDto(Guid Id);
    private sealed record InviteDto(string Token);
}
