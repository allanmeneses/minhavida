using System.Net;
using System.Net.Http.Json;
using System.Text;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class AccountDeletionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccountDeletionTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Exclusao_PedeSenhaConfirmaPorEmailEApagaDadosPessoais()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client, password: "senha-segura-1");
        client.WithToken(auth.Token);
        using (var form = TextFile("privado.txt", "dado da pessoa"))
        {
            (await client.PostAsync("/api/documents", form)).EnsureSuccessStatusCode();
        }

        var wrong = await client.PostAsJsonAsync("/api/account/deletion-requests", new { password = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var ask = await client.PostAsJsonAsync("/api/account/deletion-requests", new { password = "senha-segura-1" });
        Assert.Equal(HttpStatusCode.NoContent, ask.StatusCode);
        var token = await LastEmailToken(auth.Email, "Para apagar seus dados, use este codigo: ");

        var confirm = await client.PostAsJsonAsync("/api/account/deletion-requests/confirm", new { token });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Id == auth.UserId));
        Assert.Empty(await db.Documents.Where(x => x.UserId == auth.UserId).ToListAsync());
        var closing = await db.SentEmails.OrderByDescending(x => x.SentAt).FirstAsync(x => x.To == auth.Email && x.Subject.Contains("encerrada"));
        Assert.DoesNotContain("http", closing.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nao reativa", closing.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exclusao_TransfereCirculoParaAdminEAnonimizaLog()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono", password: "senha-segura-1");
        dono.WithToken(donoAuth.Token);
        var admin = _factory.CreateClient();
        var adminAuth = await AuthHelper.RegisterAsync(admin, name: "Admin");
        admin.WithToken(adminAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Cuidado" });
        var circle = await created.Content.ReadFromJsonAsync<CircleDto>();
        var invite = await dono.PostAsJsonAsync($"/api/circles/{circle!.Id}/invites", new { email = adminAuth.Email, role = "admin" });
        var inviteToken = (await invite.Content.ReadFromJsonAsync<InviteDto>())!.Token;
        (await admin.PostAsJsonAsync($"/api/circles/invites/{inviteToken}/accept", new { })).EnsureSuccessStatusCode();
        (await dono.PostAsJsonAsync($"/api/circles/{circle.Id}/posts", new { body = "Nota" })).EnsureSuccessStatusCode();

        (await dono.PostAsJsonAsync("/api/account/deletion-requests", new { password = "senha-segura-1" })).EnsureSuccessStatusCode();
        var token = await LastEmailToken(donoAuth.Email, "Para apagar seus dados, use este codigo: ");
        (await dono.PostAsJsonAsync("/api/account/deletion-requests/confirm", new { token })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Circles.Include(x => x.Members).Include(x => x.Logs).SingleAsync(x => x.Id == circle.Id);
        Assert.Equal(adminAuth.UserId, stored.OwnerUserId);
        Assert.DoesNotContain(stored.Members, x => x.UserId == donoAuth.UserId);
        Assert.Contains(stored.Logs, x => x.Action == "transferred");
        Assert.DoesNotContain(stored.Logs, x => x.ActorUserId == donoAuth.UserId);
        Assert.Contains(stored.Logs, x => x.Action == "posted" && x.ActorUserId == null);
    }

    [Fact]
    public async Task ExclusaoSobrepostaAoUpload_NaoDeixaAContaPelaMetade()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client, password: "senha-segura-1");
        client.WithToken(auth.Token);
        (await client.PostAsJsonAsync("/api/account/deletion-requests", new { password = "senha-segura-1" })).EnsureSuccessStatusCode();
        var token = await LastEmailToken(auth.Email, "Para apagar seus dados, use este codigo: ");

        var confirmTask = client.PostAsJsonAsync("/api/account/deletion-requests/confirm", new { token });
        using var form = TextFile("corrida.txt", "arquivo no meio da exclusao");
        var uploadTask = client.PostAsync("/api/documents", form);
        await Task.WhenAll(confirmTask, uploadTask);
        var confirm = await confirmTask;
        var upload = await uploadTask;

        Assert.True(
            confirm.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest,
            $"confirmacao devolveu {(int)confirm.StatusCode}");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userGone = !await db.Users.AnyAsync(x => x.Id == auth.UserId);
        if (userGone)
        {
            Assert.Empty(await db.Documents.Where(x => x.UserId == auth.UserId).ToListAsync());
            Assert.True(
                upload.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
                $"upload devolveu {(int)upload.StatusCode}");
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        }
    }

    private async Task<string> LastEmailToken(string to, string prefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.SentEmails.OrderByDescending(x => x.SentAt).FirstAsync(x => x.To == to);
        var index = message.Body.IndexOf(prefix, StringComparison.Ordinal);
        return message.Body[(index + prefix.Length)..].Trim();
    }

    private static MultipartFormDataContent TextFile(string name, string content)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        var form = new MultipartFormDataContent();
        form.Add(file, "file", name);
        return form;
    }

    private sealed record CircleDto(Guid Id);
    private sealed record InviteDto(string Token);
}
