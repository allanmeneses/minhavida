using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Circulos.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Circulos.Api.Tests;

public sealed class DocumentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public DocumentTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadSemToken_RetornaNaoAutorizado()
    {
        var client = _factory.CreateClient();
        using var form = CreateTextFile("nota.txt", "texto privado");
        var response = await client.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dono_EnviaListaBaixaEApagaDocumento()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        using (var form = CreateTextFile("contrato.txt", "clausula confidencial"))
        {
            var upload = await client.PostAsync("/api/documents", form);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        }

        var list = await client.GetFromJsonAsync<List<DocumentDto>>("/api/documents", Json);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("contrato.txt", list![0].FileName);
        Assert.DoesNotContain("storageKey", await (await client.GetAsync("/api/documents")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var download = await client.GetAsync($"/api/documents/{list[0].Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("clausula confidencial", await download.Content.ReadAsStringAsync());
        Assert.Contains("private", download.Headers.CacheControl?.ToString() ?? string.Empty);
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString() ?? string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(download.Content.Headers.ContentDisposition?.FileNameStar ?? download.Content.Headers.ContentDisposition?.FileName));

        var delete = await client.DeleteAsync($"/api/documents/{list[0].Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var after = await client.GetFromJsonAsync<List<DocumentDto>>("/api/documents", Json);
        Assert.NotNull(after);
        Assert.Empty(after);
        var missing = await client.GetAsync($"/api/documents/{list[0].Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task OutroUsuario_NaoListaNemBaixaNemApagaDocumentoAlheio()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);

        Guid documentId;
        using (var form = CreateTextFile("privado.txt", "so o dono ve"))
        {
            var upload = await dono.PostAsync("/api/documents", form);
            upload.EnsureSuccessStatusCode();
            var created = await upload.Content.ReadFromJsonAsync<DocumentDto>(Json);
            documentId = created!.Id;
        }

        var outro = _factory.CreateClient();
        var outroAuth = await AuthHelper.RegisterAsync(outro, name: "Outra Pessoa");
        outro.WithToken(outroAuth.Token);

        var list = await outro.GetFromJsonAsync<List<DocumentDto>>("/api/documents", Json);
        Assert.NotNull(list);
        Assert.Empty(list);
        Assert.DoesNotContain(list!, x => x.Id == documentId);

        var download = await outro.GetAsync($"/api/documents/{documentId}/download");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);

        var delete = await outro.DeleteAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var stillThere = await dono.GetFromJsonAsync<List<DocumentDto>>("/api/documents", Json);
        Assert.Single(stillThere!);
        Assert.Equal(documentId, stillThere![0].Id);
    }

    [Fact]
    public async Task DownloadSemToken_NaoExpõeArquivo()
    {
        var autenticado = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(autenticado);
        autenticado.WithToken(auth.Token);

        Guid documentId;
        using (var form = CreateTextFile("segredo.txt", "conteudo privado"))
        {
            var upload = await autenticado.PostAsync("/api/documents", form);
            var created = await upload.Content.ReadFromJsonAsync<DocumentDto>(Json);
            documentId = created!.Id;
        }

        var anonimo = _factory.CreateClient();
        var response = await anonimo.GetAsync($"/api/documents/{documentId}/download");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ArquivoSalvoNaoEstaLegivel()
    {
        const string secret = "texto-privado-que-nao-pode-aparecer-em-claro-" + "9f3c2a";
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        DocumentDto created;
        using (var form = CreateTextFile("segredo.txt", secret))
        {
            var upload = await client.PostAsync("/api/documents", form);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            created = (await upload.Content.ReadFromJsonAsync<DocumentDto>(Json))!;
        }

        var inspectable = _factory.Services.GetRequiredService<InspectableMemoryFileStorage>();
        var rawFiles = inspectable.Snapshot();
        Assert.NotEmpty(rawFiles);
        Assert.DoesNotContain(
            rawFiles.Values,
            bytes => Encoding.UTF8.GetString(bytes).Contains(secret, StringComparison.Ordinal));

        var download = await client.GetAsync($"/api/documents/{created.Id}/download");
        Assert.Equal(secret, await download.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChaveDeCifra_NaoFicaNoBanco()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        using (var form = CreateTextFile("nota.txt", "conteudo privado da nota"))
        {
            (await client.PostAsync("/api/documents", form)).EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dump = await DumpDatabaseAsync(db);
        Assert.DoesNotContain(TestEncryptionKey.Base64, dump, StringComparison.Ordinal);
        Assert.DoesNotContain("Encryption:Key", dump, StringComparison.Ordinal);
        var document = await db.Documents.AsNoTracking().SingleAsync(x => x.UserId == auth.UserId);
        Assert.False(string.IsNullOrWhiteSpace(document.StorageKey));
        Assert.DoesNotContain(TestEncryptionKey.Base64, document.StorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FotoDoPerfil_SoODonoBaixa()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);

        using (var form = CreatePngFile("foto.png"))
        {
            var upload = await dono.PostAsync("/api/profile/photo", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var jpegSignature = new byte[] { 0xFF, 0xD8 };
        var inspectable = _factory.Services.GetRequiredService<InspectableMemoryFileStorage>();
        Assert.DoesNotContain(
            inspectable.Snapshot().Values,
            bytes => bytes.AsSpan().StartsWith(pngSignature) || bytes.AsSpan().StartsWith(jpegSignature));

        var photo = await dono.GetAsync("/api/profile/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Contains("private", photo.Headers.CacheControl?.ToString() ?? string.Empty);
        var downloaded = await photo.Content.ReadAsByteArrayAsync();
        Assert.True(downloaded.AsSpan().StartsWith(jpegSignature));

        var outro = _factory.CreateClient();
        var outroAuth = await AuthHelper.RegisterAsync(outro, name: "Visitante");
        outro.WithToken(outroAuth.Token);
        var alheia = await outro.GetAsync("/api/profile/photo");
        Assert.Equal(HttpStatusCode.NotFound, alheia.StatusCode);
    }

    private static MultipartFormDataContent CreateTextFile(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", name);
        return form;
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

    private static async Task<string> DumpDatabaseAsync(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var builder = new StringBuilder();
        await using (var tables = connection.CreateCommand())
        {
            tables.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'public'";
            await using var reader = await tables.ExecuteReaderAsync();
            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            await reader.DisposeAsync();
            foreach (var name in names)
            {
                if (name.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
                {
                    continue;
                }

                await using var rows = connection.CreateCommand();
                rows.CommandText = $"SELECT * FROM \"{name}\"";
                await using var data = await rows.ExecuteReaderAsync();
                while (await data.ReadAsync())
                {
                    for (var i = 0; i < data.FieldCount; i++)
                    {
                        builder.Append(data.IsDBNull(i) ? string.Empty : data.GetValue(i));
                        builder.Append('|');
                    }
                }
            }
        }

        return builder.ToString();
    }

    private sealed record DocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);
}
