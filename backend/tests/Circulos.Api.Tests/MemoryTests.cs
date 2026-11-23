using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Circulos.Api.Tests;

public sealed class MemoryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public MemoryTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Perfil_CriaLinhaDoTempoEEdicaoSilenciosaNaoApagaTexto()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var created = await client.PostAsJsonAsync("/api/memories", new
        {
            title = "Primeiro dia de escola",
            body = "Foto da entrada",
            occurredOn = "2018-02-05",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var memory = await created.Content.ReadFromJsonAsync<MemoryDto>(Json);
        var listed = await client.GetFromJsonAsync<List<MemoryDto>>("/api/memories", Json);
        Assert.Contains(listed!, x => x.Id == memory!.Id && x.Title == "Primeiro dia de escola");

        var edited = await client.PutAsJsonAsync($"/api/memories/{memory!.Id}", new
        {
            title = "Primeiro dia de escola",
            body = "Texto que a pessoa digitou",
            occurredOn = "2018-02-05",
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var after = await edited.Content.ReadFromJsonAsync<MemoryDto>(Json);
        Assert.Equal("Texto que a pessoa digitou", after!.Body);
        Assert.Null(after.CircleId);
    }

    [Fact]
    public async Task Circulo_EditorCria_ViewerRecebe403_VisitanteNaoVe()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var editor = _factory.CreateClient();
        var editorAuth = await AuthHelper.RegisterAsync(editor, name: "Editor");
        editor.WithToken(editorAuth.Token);
        var viewer = _factory.CreateClient();
        var viewerAuth = await AuthHelper.RegisterAsync(viewer, name: "Viewer");
        viewer.WithToken(viewerAuth.Token);
        var visitante = _factory.CreateClient();
        visitante.WithToken((await AuthHelper.RegisterAsync(visitante, name: "Visitante")).Token);

        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Família album" })).Content.ReadFromJsonAsync<CircleDto>(Json);
        var inviteEditor = await (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = editorAuth.Email, role = "editor" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await editor.PostAsJsonAsync($"/api/circles/invites/{inviteEditor!.Token}/accept", new { })).EnsureSuccessStatusCode();
        var inviteViewer = await (await dono.PostAsJsonAsync($"/api/circles/{circle.Id}/invites", new { email = viewerAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<InviteDto>(Json);
        (await viewer.PostAsJsonAsync($"/api/circles/invites/{inviteViewer!.Token}/accept", new { })).EnsureSuccessStatusCode();

        var created = await editor.PostAsJsonAsync("/api/memories", new { title = "Almoco", body = "Mesa posta", circleId = circle.Id });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/memories", new { title = "Não", circleId = circle.Id })).StatusCode);

        var forViewer = await viewer.GetFromJsonAsync<List<MemoryDto>>($"/api/memories?circleId={circle.Id}", Json);
        Assert.Contains(forViewer!, x => x.Title == "Almoco");
        Assert.Equal(HttpStatusCode.NotFound, (await visitante.GetAsync($"/api/memories?circleId={circle.Id}")).StatusCode);
    }

    [Fact]
    public async Task Surpresa_HomenageadoNaoVeMemoriaAntesDaRevelacao()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var honrado = _factory.CreateClient();
        var honradoAuth = await AuthHelper.RegisterAsync(honrado, name: "Aniversariante");
        honrado.WithToken(honradoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Festa album",
            isSurprise = true,
            surpriseSubjectEmail = honradoAuth.Email,
        })).Content.ReadFromJsonAsync<CircleDto>(Json);
        (await dono.PostAsJsonAsync("/api/memories", new { title = "Foto secreta", circleId = circle!.Id })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await honrado.GetAsync($"/api/memories?circleId={circle.Id}")).StatusCode);
    }

    [Fact]
    public async Task KeepMemory_SobrepostoDoMesmoUsuario_FicaUmaSo()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Surpresa keep", isSurprise = true })).Content.ReadFromJsonAsync<CircleDto>(Json);
        (await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/posts", new { body = "Parabens" })).EnsureSuccessStatusCode();
        (await dono.PostAsync($"/api/circles/{circle.Id}/reveal", null)).EnsureSuccessStatusCode();

        var a = _factory.CreateClient();
        a.WithToken(donoAuth.Token);
        var b = _factory.CreateClient();
        b.WithToken(donoAuth.Token);
        var responses = await Task.WhenAll(
            a.PostAsync($"/api/circles/{circle.Id}/keep-memory", null),
            b.PostAsync($"/api/circles/{circle.Id}/keep-memory", null));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Memories.CountAsync(x => x.SourceCircleId == circle.Id && x.AuthorUserId == donoAuth.UserId);
        Assert.Equal(1, count);
        var saved = await db.Memories.SingleAsync(x => x.SourceCircleId == circle.Id);
        Assert.Contains("Parabens", saved.Body);
    }

    [Fact]
    public async Task FotoDaMemoria_ComprimeEFicaDisponivelNoAlbum()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var created = await (await client.PostAsJsonAsync("/api/memories", new { title = "Album", body = "Foto" }))
            .Content.ReadFromJsonAsync<MemoryDto>(Json);
        using var form = CreatePngFile("album.png");
        var upload = await client.PostAsync($"/api/memories/{created!.Id}/photo", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var body = await upload.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("hasPhoto").GetBoolean());
        var photo = await client.GetAsync($"/api/memories/{created.Id}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
    }

    private static MultipartFormDataContent CreatePngFile(string name)
    {
        using var image = new SKBitmap(8, 8, SKColorType.Rgba8888, SKAlphaType.Opaque);
        image.Erase(SKColors.SteelBlue);
        using var encoded = SKImage.FromBitmap(image).Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Falha ao gerar o PNG de teste.");
        var file = new ByteArrayContent(encoded.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", name);
        return form;
    }

    private sealed record MemoryDto(Guid Id, Guid AuthorUserId, Guid? CircleId, Guid? SourceCircleId, string Title, string Body);
    private sealed record CircleDto(Guid Id);
    private sealed record InviteDto(string Token);
}
