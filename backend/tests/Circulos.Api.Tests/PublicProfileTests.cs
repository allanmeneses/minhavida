using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;

namespace Circulos.Api.Tests;

public sealed class PublicProfileTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public PublicProfileTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PerfilPublico_VisitanteVeNomeEFoto_PrivadoDa404()
    {
        var dono = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(dono, name: "Ana Costa");
        dono.WithToken(auth.Token);
        using (var form = CreatePngFile("foto.png"))
        {
            Assert.Equal(HttpStatusCode.OK, (await dono.PostAsync("/api/profile/photo", form)).StatusCode);
        }

        var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/public/profiles/{auth.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/public/profiles/{auth.UserId}/photo")).StatusCode);

        var opened = await dono.PutAsJsonAsync("/api/profile", new { displayName = "Ana Costa", isPublic = true, bio = "Corro de manhã" });
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);

        var publico = await anonimo.GetFromJsonAsync<JsonElement>($"/api/public/profiles/{auth.UserId}", Json);
        Assert.Equal("Ana Costa", publico.GetProperty("displayName").GetString());
        Assert.True(publico.GetProperty("hasPhoto").GetBoolean());
        Assert.Equal("Corro de manhã", publico.GetProperty("description").GetString());
        Assert.False(publico.TryGetProperty("phone", out _));
        Assert.False(publico.TryGetProperty("secondaryEmail", out _));
        Assert.False(publico.TryGetProperty("birthDate", out _));

        var photo = await anonimo.GetAsync($"/api/public/profiles/{auth.UserId}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.True((await photo.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith(new byte[] { 0xFF, 0xD8 }));
    }

    [Fact]
    public async Task Perfil_VoltaPrivado_SomeDaVisaoPublica()
    {
        var dono = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(dono, name: "Bruno");
        dono.WithToken(auth.Token);
        (await dono.PutAsJsonAsync("/api/profile", new { displayName = "Bruno", isPublic = true })).EnsureSuccessStatusCode();
        var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync($"/api/public/profiles/{auth.UserId}")).StatusCode);

        (await dono.PutAsJsonAsync("/api/profile", new { displayName = "Bruno", isPublic = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/public/profiles/{auth.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/public/profiles/{auth.UserId}/photo")).StatusCode);
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
}
