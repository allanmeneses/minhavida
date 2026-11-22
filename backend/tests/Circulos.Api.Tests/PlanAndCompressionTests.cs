using System.Net;
using System.Net.Http.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Circulos.Api.Tests;

public sealed class PlanAndCompressionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PlanAndCompressionTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Plano_MostraLimitesSemGateway()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var plan = await client.GetFromJsonAsync<JsonPlan>("/api/plan");
        Assert.Equal("free", plan!.Plan);
        Assert.Equal(3, plan.CircleLimit);
        Assert.Equal(PlanLimits.FreeStorageBytes, plan.StorageLimitBytes);
        Assert.Contains("Mercado Pago", plan.Checkout, StringComparison.OrdinalIgnoreCase);

        var checkout = await client.PostAsJsonAsync("/api/plan/checkout", new { cardToken = "fake-cartao-aprovado" });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
    }

    [Fact]
    public async Task Armazenamento_DoisEnviosSobrepostosRespeitamOTeto()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(x => x.Id == auth.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StorageUsedBytes, PlanLimits.FreeStorageBytes - 12));
        }

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        using var formA = TextFile("a.txt", "12345678");
        using var formB = TextFile("b.txt", "12345678");
        var responses = await Task.WhenAll(a.PostAsync("/api/documents", formA), b.PostAsync("/api/documents", formB));
        var codes = responses.Select(x => x.StatusCode).ToArray();
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.BadRequest, codes);

        using var check = _factory.Services.CreateScope();
        var users = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await users.Users.SingleAsync(x => x.Id == auth.UserId);
        Assert.True(user.StorageUsedBytes <= PlanLimits.FreeStorageBytes);
        Assert.True(PlanLimits.ShouldWarn(user.StorageUsedBytes, PlanLimits.FreeStorageBytes));
    }

    [Fact]
    public async Task Foto_ComprimeSubstituiOriginalEFicaCifrada()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        using var image = new SKBitmap(1600, 1200, SKColorType.Rgba8888, SKAlphaType.Opaque);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image.SetPixel(x, y, new SKColor((byte)(x % 255), (byte)(y % 255), (byte)((x + y) % 255)));
            }
        }
        using var pngImage = SKImage.FromBitmap(image);
        using var pngData = pngImage.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Falha ao gerar o PNG de teste.");
        using var file = new ByteArrayContent(pngData.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        using var form = new MultipartFormDataContent();
        form.Add(file, "file", "grande.png");
        var upload = await client.PostAsync("/api/profile/photo", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var photo = await client.GetAsync("/api/profile/photo");
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        var jpeg = await photo.Content.ReadAsByteArrayAsync();
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);
        using var decoded = SKBitmap.Decode(jpeg);
        Assert.NotNull(decoded);
        Assert.True(Math.Max(decoded.Width, decoded.Height) <= ImageCompressor.MaxEdge);

        var inspectable = _factory.Services.GetRequiredService<InspectableMemoryFileStorage>();
        Assert.DoesNotContain(inspectable.Snapshot().Values, bytes => bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.DoesNotContain(inspectable.Snapshot().Values, bytes => bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8);
    }

    [Fact]
    public async Task Atalho_ExecutaSemConfirmacaoEApareceNoPainel()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var created = await client.PostAsJsonAsync("/api/shortcuts", new { label = "Tomei o remedio", color = "#C45C26" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var shortcut = await created.Content.ReadFromJsonAsync<ShortcutDto>();
        var run = await client.PostAsync($"/api/shortcuts/{shortcut!.Id}/run", null);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        var body = await run.Content.ReadAsStringAsync();
        Assert.Contains("Tomei o remedio", body);

        var home = await client.GetFromJsonAsync<JsonHome>("/api/home");
        Assert.Contains(home!.Shortcuts, x => x.Id == shortcut.Id);
        Assert.Equal(0, home.ActiveCircleCount);
    }

    [Fact]
    public async Task Atalho_DesativarSomeDaPrincipalEExcluirMantemHistorico()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var created = await client.PostAsJsonAsync("/api/shortcuts", new { label = "Tomei o remedio" });
        var shortcut = await created.Content.ReadFromJsonAsync<ShortcutDto>();
        (await client.PostAsync($"/api/shortcuts/{shortcut!.Id}/run", null)).EnsureSuccessStatusCode();

        var deactivate = await client.PostAsync($"/api/shortcuts/{shortcut.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var home = await client.GetFromJsonAsync<JsonHome>("/api/home");
        Assert.DoesNotContain(home!.Shortcuts, x => x.Id == shortcut.Id);
        var listed = await client.GetFromJsonAsync<List<ShortcutDto>>("/api/shortcuts");
        Assert.Contains(listed!, x => x.Id == shortcut.Id);

        var activate = await client.PostAsync($"/api/shortcuts/{shortcut.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        var homeOn = await client.GetFromJsonAsync<JsonHome>("/api/home");
        Assert.Contains(homeOn!.Shortcuts, x => x.Id == shortcut.Id);

        var delete = await client.DeleteAsync($"/api/shortcuts/{shortcut.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var areas = await client.GetFromJsonAsync<List<JsonArea>>("/api/life-areas");
        var diary = areas!.Single(x => x.Kind == "diary");
        var entries = await client.GetFromJsonAsync<List<JsonEntry>>($"/api/life-areas/{diary.Id}/entries");
        Assert.Contains(entries!, x => x.Title == "Tomei o remedio");
    }

    private static MultipartFormDataContent TextFile(string name, string content)
    {
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", name);
        return form;
    }

    private sealed record JsonPlan(string Plan, int? CircleLimit, long StorageLimitBytes, string Checkout);
    private sealed record ShortcutDto(Guid Id, string Label);
    private sealed record JsonHome(int ActiveCircleCount, List<ShortcutDto> Shortcuts);
    private sealed record JsonArea(Guid Id, string Kind);
    private sealed record JsonEntry(string Title);
}
