using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class Rodada14Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Rodada14Tests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Sistema_NaoTemPrazoDeInatividade()
    {
        Assert.False(MembershipRules.ConsidersMemberInactiveByTime);
    }

    [Fact]
    public async Task MembroSemAtividadeHaMuitoTempo_ContinuaMembroComMesmosDireitos()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var quieto = _factory.CreateClient();
        var quietoAuth = await AuthHelper.RegisterAsync(quieto, name: "Quieto");
        quieto.WithToken(quietoAuth.Token);
        var ativo = _factory.CreateClient();
        var ativoAuth = await AuthHelper.RegisterAsync(ativo, name: "Ativo");
        ativo.WithToken(ativoAuth.Token);

        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Casa longa", visibility = "private" });
        var circleId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await InviteAsync(dono, quieto, quietoAuth.Email, circleId, "editor");
        await InviteAsync(dono, ativo, ativoAuth.Email, circleId, "editor");
        (await ativo.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "Hoje" })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var joined = DateTime.UtcNow.AddDays(-400);
            await db.CircleMembers.Where(item => item.CircleId == circleId && item.UserId == quietoAuth.UserId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.JoinedAt, joined));
            await db.Users.Where(item => item.Id == quietoAuth.UserId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CreatedAt, joined));
        }

        var post = await quieto.PostAsJsonAsync($"/api/circles/{circleId}/posts", new { body = "Ainda posso" });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await quieto.GetAsync($"/api/export/circles/{circleId}")).StatusCode);

        var detail = await quieto.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}", Json);
        var members = detail.GetProperty("members").EnumerateArray().ToList();
        var quietoMember = members.Single(item => item.GetProperty("userId").GetGuid() == quietoAuth.UserId);
        var ativoMember = members.Single(item => item.GetProperty("userId").GetGuid() == ativoAuth.UserId);
        Assert.Equal("editor", quietoMember.GetProperty("role").GetString());
        Assert.Equal("editor", ativoMember.GetProperty("role").GetString());
        Assert.Contains(detail.GetProperty("posts").EnumerateArray(), item => item.GetProperty("body").GetString() == "Ainda posso");
    }

    [Fact]
    public async Task SemDono_39Dias_NaoOferecePosse()
    {
        var (circleId, members) = await CircleWithEditorsAsync(3);
        await members[0].Client.DeleteAsync($"/api/circles/{circleId}/membership");
        await SetOwnerLostAsync(circleId, daysAgo: 39);

        members[1].Client.WithToken(members[1].Auth.Token);
        (await members[1].Client.PostAsJsonAsync("/api/jobs", new { })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.OwnershipOffers.Where(item => item.CircleId == circleId).ToListAsync());
        var circle = await db.Circles.SingleAsync(item => item.Id == circleId);
        Assert.Null(circle.OwnerUserId);
        Assert.Equal(CircleStatus.Active, circle.Status);
    }

    [Fact]
    public async Task SemDono_40Dias_OfereceAsTresMaisAtivas()
    {
        var (circleId, members) = await CircleWithEditorsAsync(5);
        for (var i = 0; i < members.Count; i++)
        {
            for (var n = 0; n < 5 - i; n++)
            {
                (await members[i].Client.PostAsJsonAsync(
                    $"/api/circles/{circleId}/posts",
                    new { body = $"Post {i}-{n}" })).EnsureSuccessStatusCode();
            }
        }

        await members[0].Client.DeleteAsync($"/api/circles/{circleId}/membership");
        await SetOwnerLostAsync(circleId, daysAgo: 40);
        members[1].Client.WithToken(members[1].Auth.Token);
        (await members[1].Client.PostAsJsonAsync("/api/jobs", new { })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var offers = await db.OwnershipOffers.Where(item => item.CircleId == circleId).ToListAsync();
        Assert.Equal(3, offers.Count);
        Assert.All(offers, offer => Assert.Equal(OwnershipOfferStatus.Pending, offer.Status));
        var offered = offers.Select(item => item.UserId).ToHashSet();
        Assert.Contains(members[1].Auth.UserId, offered);
        Assert.Contains(members[2].Auth.UserId, offered);
        Assert.Contains(members[3].Auth.UserId, offered);
        Assert.DoesNotContain(members[4].Auth.UserId, offered);
        Assert.DoesNotContain(members[0].Auth.UserId, offered);

        var emails = await db.SentEmails
            .Where(item => item.Subject.Contains("assumir o circulo"))
            .Select(item => item.To)
            .ToListAsync();
        Assert.Contains(members[1].Auth.Email, emails);
        Assert.Contains(members[2].Auth.Email, emails);
        Assert.Contains(members[3].Auth.Email, emails);
    }

    [Fact]
    public async Task OfertaDePosse_PrimeiroAceiteViraDono_SegundoFalha()
    {
        var (circleId, a, b) = await OfferedPairAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.PostAsJsonAsync($"/api/circles/{circleId}/ownership-offer/accept", new { })).StatusCode);
        var second = await b.Client.PostAsJsonAsync($"/api/circles/{circleId}/ownership-offer/accept", new { });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("nao esta mais aberta", await second.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var circle = await db.Circles.Include(item => item.Members).SingleAsync(item => item.Id == circleId);
        Assert.Equal(a.Auth.UserId, circle.OwnerUserId);
        Assert.Equal(CircleStatus.Active, circle.Status);
        Assert.Equal(CircleRole.Owner, circle.Members.Single(item => item.UserId == a.Auth.UserId).Role);
        Assert.Equal(1, await db.OwnershipOffers.CountAsync(item => item.CircleId == circleId && item.Status == OwnershipOfferStatus.Accepted));
        Assert.Equal(1, await db.OwnershipOffers.CountAsync(item => item.CircleId == circleId && item.Status == OwnershipOfferStatus.Cancelled));
    }

    [Fact]
    public async Task OfertaDePosse_DoisAceitesSobrepostos_SoUmViraDono()
    {
        var (circleId, a, b) = await OfferedPairAsync();
        var first = a.Client.PostAsJsonAsync($"/api/circles/{circleId}/ownership-offer/accept", new { });
        var second = b.Client.PostAsJsonAsync($"/api/circles/{circleId}/ownership-offer/accept", new { });
        var responses = await Task.WhenAll(first, second);
        var codes = responses.Select(item => item.StatusCode).OrderBy(item => item).ToArray();
        Assert.Contains(HttpStatusCode.NoContent, codes);
        Assert.Contains(HttpStatusCode.BadRequest, codes);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var circle = await db.Circles.Include(item => item.Members).SingleAsync(item => item.Id == circleId);
        Assert.True(circle.OwnerUserId == a.Auth.UserId || circle.OwnerUserId == b.Auth.UserId);
        Assert.Equal(1, circle.Members.Count(item => item.Role == CircleRole.Owner));
        Assert.Equal(1, await db.OwnershipOffers.CountAsync(item => item.CircleId == circleId && item.Status == OwnershipOfferStatus.Accepted));
        Assert.Equal(1, await db.OwnershipOffers.CountAsync(item => item.CircleId == circleId && item.Status == OwnershipOfferStatus.Cancelled));
    }

    [Fact]
    public async Task EleicaoAntesDe40Dias_CancelaOferta()
    {
        var (circleId, members) = await CircleWithEditorsAsync(3);
        await members[0].Client.DeleteAsync($"/api/circles/{circleId}/membership");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await members[1].Client.PostAsJsonAsync(
                $"/api/circles/{circleId}/succession/votes",
                new { candidateUserId = members[1].Auth.UserId })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await members[2].Client.PostAsJsonAsync(
                $"/api/circles/{circleId}/succession/votes",
                new { candidateUserId = members[1].Auth.UserId })).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var circle = await db.Circles.SingleAsync(item => item.Id == circleId);
            Assert.Equal(members[1].Auth.UserId, circle.OwnerUserId);
            circle.OwnershipNextCheckAt = DateTime.UtcNow.AddDays(-1);
            circle.OwnerLostAt = DateTime.UtcNow.AddDays(-40);
            await db.SaveChangesAsync();
        }

        (await members[1].Client.PostAsJsonAsync("/api/jobs", new { })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.OwnershipOffers.Where(item => item.CircleId == circleId).ToListAsync());
            var circle = await db.Circles.SingleAsync(item => item.Id == circleId);
            Assert.Equal(members[1].Auth.UserId, circle.OwnerUserId);
        }
    }

    [Fact]
    public async Task SemDono_MenosDeTresMembros_OfereceAQuemExiste()
    {
        var (circleId, members) = await CircleWithEditorsAsync(2);
        await members[0].Client.DeleteAsync($"/api/circles/{circleId}/membership");
        await SetOwnerLostAsync(circleId, daysAgo: 40);
        (await members[1].Client.PostAsJsonAsync("/api/jobs", new { })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var offers = await db.OwnershipOffers.Where(item => item.CircleId == circleId).ToListAsync();
        Assert.Single(offers);
        Assert.Equal(members[1].Auth.UserId, offers[0].UserId);
        Assert.Equal(OwnershipOfferStatus.Pending, offers[0].Status);
    }

    [Fact]
    public async Task CirculoVazioSemDono_40Dias_NaoInventaDono()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono", password: "senha-segura-1");
        dono.WithToken(donoAuth.Token);
        var created = await dono.PostAsJsonAsync("/api/circles", new { name = "Sozinho" });
        var circleId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        (await dono.PostAsJsonAsync("/api/account/deletion-requests", new { password = "senha-segura-1" })).EnsureSuccessStatusCode();
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = await db.SentEmails.OrderByDescending(item => item.SentAt).FirstAsync(item => item.To == donoAuth.Email);
            const string prefix = "Para apagar seus dados, use este codigo: ";
            var index = message.Body.IndexOf(prefix, StringComparison.Ordinal);
            token = message.Body[(index + prefix.Length)..].Trim();
        }

        (await dono.PostAsJsonAsync("/api/account/deletion-requests/confirm", new { token })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.Include(item => item.Logs).SingleAsync(item => item.Id == circleId);
            Assert.Null(stored.OwnerUserId);
            Assert.Equal(CircleStatus.Active, stored.Status);
            Assert.NotNull(stored.OwnerLostAt);
            Assert.NotNull(stored.OwnershipNextCheckAt);
            Assert.DoesNotContain(stored.Logs, item => item.Action == "locked");
        }

        await SetOwnerLostAsync(circleId, daysAgo: 40);
        var spectator = _factory.CreateClient();
        spectator.WithToken((await AuthHelper.RegisterAsync(spectator)).Token);
        (await spectator.PostAsJsonAsync("/api/jobs", new { })).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.OwnershipOffers.Where(item => item.CircleId == circleId).ToListAsync());
            var stored = await db.Circles.SingleAsync(item => item.Id == circleId);
            Assert.Equal(CircleStatus.Active, stored.Status);
            Assert.Null(stored.OwnerUserId);
        }
    }

    private async Task<(Guid CircleId, List<(HttpClient Client, AuthHelper.AuthResult Auth)> Members)> CircleWithEditorsAsync(int count)
    {
        var members = new List<(HttpClient Client, AuthHelper.AuthResult Auth)>();
        for (var i = 0; i < count; i++)
        {
            var client = _factory.CreateClient();
            var auth = await AuthHelper.RegisterAsync(client, name: $"Pessoa {i}");
            client.WithToken(auth.Token);
            members.Add((client, auth));
        }

        var created = await members[0].Client.PostAsJsonAsync("/api/circles", new { name = "Grupo posse", visibility = "public" });
        var circleId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        for (var i = 1; i < members.Count; i++)
        {
            await InviteAsync(members[0].Client, members[i].Client, members[i].Auth.Email, circleId, "editor");
        }

        return (circleId, members);
    }

    private async Task<(Guid CircleId, (HttpClient Client, AuthHelper.AuthResult Auth) A, (HttpClient Client, AuthHelper.AuthResult Auth) B)> OfferedPairAsync()
    {
        var (circleId, members) = await CircleWithEditorsAsync(3);
        await members[0].Client.DeleteAsync($"/api/circles/{circleId}/membership");
        await SetOwnerLostAsync(circleId, daysAgo: 40);
        (await members[1].Client.PostAsJsonAsync("/api/jobs", new { })).EnsureSuccessStatusCode();
        return (circleId, members[1], members[2]);
    }

    private async Task SetOwnerLostAsync(Guid circleId, int daysAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lostAt = DateTime.UtcNow.AddDays(-daysAgo);
        var circle = await db.Circles.SingleAsync(item => item.Id == circleId);
        circle.OwnerLostAt = lostAt;
        circle.OwnershipNextCheckAt = lostAt.AddDays(OwnershipOfferService.WaitDays);
        await db.SaveChangesAsync();
    }

    private static async Task InviteAsync(HttpClient owner, HttpClient member, string email, Guid circleId, string role)
    {
        var invite = await (await owner.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email, role }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await member.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { }))
            .EnsureSuccessStatusCode();
    }
}
