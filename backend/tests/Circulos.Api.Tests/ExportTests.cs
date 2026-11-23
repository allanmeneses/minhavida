using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class ExportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ExportTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Perfil_ExportaJsonEArquivo()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        using var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("conteúdo secreto do documento"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(bytes, "file", "nota.txt");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/documents", content)).StatusCode);

        var export = await client.GetAsync("/api/export/me");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/zip", export.Content.Headers.ContentType?.MediaType);

        using var zip = new ZipArchive(await export.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        var dados = zip.GetEntry("dados.json");
        Assert.NotNull(dados);
        using var stream = dados!.Open();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal("profile", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(auth.Email, doc.RootElement.GetProperty("profile").GetProperty("email").GetString());
        Assert.Equal("nota.txt", doc.RootElement.GetProperty("documents")[0].GetProperty("fileName").GetString());
        var file = zip.Entries.First(x => x.FullName.StartsWith("files/documents/", StringComparison.Ordinal));
        using var fileStream = file.Open();
        using var reader = new StreamReader(fileStream, Encoding.UTF8);
        Assert.Equal("conteúdo secreto do documento", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Circulo_MembroExporta_VisitanteNao()
    {
        var owner = _factory.CreateClient();
        var visitor = _factory.CreateClient();
        var ownerAuth = await AuthHelper.RegisterAsync(owner, name: "Dono");
        owner.WithToken(ownerAuth.Token);
        var visitorAuth = await AuthHelper.RegisterAsync(visitor, name: "Visitante");
        visitor.WithToken(visitorAuth.Token);

        var created = await owner.PostAsJsonAsync("/api/circles", new { name = "Casa", visibility = "private" });
        var circle = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();

        var allowed = await owner.GetAsync($"/api/export/circles/{circleId}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var zip = new ZipArchive(await allowed.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("dados.json")!.Open();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal("circle", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal("Casa", doc.RootElement.GetProperty("circle").GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/export/circles/{circleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/export/me")).StatusCode);
    }
}
