using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;

namespace Circulos.Api.Tests;

public sealed class LifeAreaTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public LifeAreaTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cadastro_CriaPerfilPrivadoComAreasSugeridas()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/profile", Json);
        Assert.False(profile.GetProperty("isPublic").GetBoolean());

        var areas = await client.GetFromJsonAsync<List<AreaDto>>("/api/life-areas", Json);
        Assert.NotNull(areas);
        Assert.Equal(4, areas!.Count);
        Assert.Contains(areas, x => x.Kind == "vaccine" && x.Name == "Vacinas" && x.Suggested);
        Assert.Contains(areas, x => x.Kind == "reminder");
        Assert.Contains(areas, x => x.Kind == "document");
        Assert.Contains(areas, x => x.Kind == "diary");
    }

    [Fact]
    public async Task Pessoa_CriaAreaNovaEVacinaComQuandoEOnde()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        var created = await client.PostAsJsonAsync("/api/life-areas", new { name = "Corrida" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var custom = await created.Content.ReadFromJsonAsync<AreaDto>(Json);
        Assert.Equal("custom", custom!.Kind);

        var areas = await client.GetFromJsonAsync<List<AreaDto>>("/api/life-areas", Json);
        var vacinas = areas!.Single(x => x.Kind == "vaccine");
        var entry = await client.PostAsJsonAsync($"/api/life-areas/{vacinas.Id}/entries", new
        {
            title = "Hepatite B",
            occurredOn = "2026-02-01",
            location = "UBS Centro",
            body = "Primeira dose",
        });
        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
        using var doc = JsonDocument.Parse(await entry.Content.ReadAsStringAsync());
        Assert.Equal("Hepatite B", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("UBS Centro", doc.RootElement.GetProperty("location").GetString());
        Assert.Equal("2026-02-01", doc.RootElement.GetProperty("occurredOn").GetString());
    }

    [Fact]
    public async Task Diario_ExportaTextoCorrido()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var areas = await client.GetFromJsonAsync<List<AreaDto>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.Kind == "diary");
        await client.PostAsJsonAsync($"/api/life-areas/{diario.Id}/entries", new
        {
            title = "Ideia",
            body = "Texto livre do dia",
        });

        var export = await client.GetAsync($"/api/life-areas/{diario.Id}/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var text = await export.Content.ReadAsStringAsync();
        Assert.Contains("Ideia", text);
        Assert.Contains("Texto livre do dia", text);
    }

    [Fact]
    public async Task Entrada_GuardaNumeroEFoto()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var areas = await client.GetFromJsonAsync<List<AreaDto>>("/api/life-areas", Json);
        var diario = areas!.Single(x => x.Kind == "diary");
        var created = await client.PostAsJsonAsync($"/api/life-areas/{diario.Id}/entries", new
        {
            title = "Corrida",
            numberValue = 200,
            body = "200 metros",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(200, doc.RootElement.GetProperty("numberValue").GetDecimal());
        var entryId = doc.RootElement.GetProperty("id").GetGuid();

        using (var form = CreatePngFile("corrida.png"))
        {
            var upload = await client.PostAsync($"/api/life-areas/{diario.Id}/entries/{entryId}/photo", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        var photo = await client.GetAsync($"/api/life-areas/{diario.Id}/entries/{entryId}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        var bytes = await photo.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8 }));
    }

    [Fact]
    public async Task Perfil_PodeVirarPublico()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var response = await client.PutAsJsonAsync("/api/profile", new
        {
            displayName = "Ana Costa",
            isPublic = true,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("isPublic").GetBoolean());
    }

    [Fact]
    public async Task FotoDoPerfil_SomeQuandoAPessoaApaga()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        using (var form = CreatePngFile("foto.png"))
        {
            var upload = await client.PostAsync("/api/profile/photo", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        var deleted = await client.DeleteAsync("/api/profile/photo");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using var doc = JsonDocument.Parse(await deleted.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("hasPhoto").GetBoolean());
        var get = await client.GetAsync("/api/profile/photo");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
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

    private sealed record AreaDto(Guid Id, string Name, string Kind, int SortOrder, bool Suggested);
}
