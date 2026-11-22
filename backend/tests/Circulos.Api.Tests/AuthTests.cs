using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Circulos.Api.Tests;

public sealed class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public AuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cadastro_ComDadosValidos_RetornaTokenECriaPerfil()
    {
        var client = _factory.CreateClient();
        var email = $"ana-{Guid.NewGuid():N}@exemplo.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "senha-segura-1",
            name = "Ana Costa",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
        Assert.Equal(email, payload!.User.Email);
        Assert.Equal("Ana Costa", payload.User.Name);

        client.WithToken(payload.Token);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var profile = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var doc = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.Equal("Ana Costa", doc.RootElement.GetProperty("displayName").GetString());
        Assert.False(doc.RootElement.GetProperty("hasPhoto").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("isPublic").GetBoolean());
    }

    [Fact]
    public async Task Cadastro_EmailDuplicado_RetornaConflito()
    {
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@exemplo.com";
        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "senha-segura-1",
            name = "Primeira Pessoa",
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "outra-senha-1",
            name = "Segunda Pessoa",
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Cadastro_EmailDuplicadoConcorrente_RetornaConflitoSemErroInterno()
    {
        var primeiro = _factory.CreateClient();
        var segundo = _factory.CreateClient();
        var email = $"race-{Guid.NewGuid():N}@exemplo.com";
        var body = new
        {
            email,
            password = "senha-segura-1",
            name = "Pessoa",
        };

        var tarefa1 = primeiro.PostAsJsonAsync("/api/auth/register", body);
        var tarefa2 = segundo.PostAsJsonAsync("/api/auth/register", body);
        var respostas = await Task.WhenAll(tarefa1, tarefa2);

        var codes = respostas.Select(r => r.StatusCode).ToArray();
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.Conflict, codes);
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, codes);

        var conflito = respostas.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        using var doc = JsonDocument.Parse(await conflito.Content.ReadAsStringAsync());
        Assert.Equal("Ja existe uma conta com este e-mail.", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Cadastro_SenhaCurta_RetornaErroDeValidacao()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"curta-{Guid.NewGuid():N}@exemplo.com",
            password = "123",
            name = "Pessoa",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaToken()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@exemplo.com";
        await AuthHelper.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "senha-segura-1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthHelper.AuthResponseDto>(Json);
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
    }

    [Fact]
    public async Task Login_SenhaErrada_RetornaNaoAutorizado()
    {
        var client = _factory.CreateClient();
        var email = $"errado-{Guid.NewGuid():N}@exemplo.com";
        await AuthHelper.RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "senha-errada-99",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_SemToken_RetornaNaoAutorizado()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Perfil_AtualizaDadosBasicos()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);

        var response = await client.PutAsJsonAsync("/api/profile", new
        {
            displayName = "Ana Costa",
            birthDate = "1994-03-12",
            phone = "+55 11 99999-0000",
            secondaryEmail = "ana.contato@exemplo.com",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Ana Costa", doc.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("1994-03-12", doc.RootElement.GetProperty("birthDate").GetString());
        Assert.Equal("+55 11 99999-0000", doc.RootElement.GetProperty("phone").GetString());
        Assert.Equal("ana.contato@exemplo.com", doc.RootElement.GetProperty("secondaryEmail").GetString());
    }
}
