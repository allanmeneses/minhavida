using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class SuccessionAndNearbyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public SuccessionAndNearbyTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Votacao_MaioriaSimplesEmTresDiasElege_EPrazoSemMaioriaTrava()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var a = _factory.CreateClient();
        var aAuth = await AuthHelper.RegisterAsync(a, name: "Ana");
        a.WithToken(aAuth.Token);
        var b = _factory.CreateClient();
        var bAuth = await AuthHelper.RegisterAsync(b, name: "Bia");
        b.WithToken(bAuth.Token);

        var circle = await (await dono.PostAsJsonAsync("/api/circles", new { name = "Casa", visibility = "public" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var circleId = circle.GetProperty("id").GetGuid();
        var inviteA = await (await dono.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email = aAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var inviteB = await (await dono.PostAsJsonAsync($"/api/circles/{circleId}/invites", new { email = bAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        (await a.PostAsJsonAsync($"/api/circles/invites/{inviteA.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();
        (await b.PostAsJsonAsync($"/api/circles/invites/{inviteB.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await dono.DeleteAsync($"/api/circles/{circleId}/membership")).StatusCode);

        var vote = await a.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("open", vote.GetProperty("status").GetString());
        Assert.True(vote.GetProperty("deadlineAt").GetDateTime() > DateTime.UtcNow.AddDays(2));
        Assert.True(vote.GetProperty("deadlineAt").GetDateTime() <= DateTime.UtcNow.AddDays(3).AddMinutes(1));

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = aAuth.UserId })).StatusCode);
        var stillOpen = await a.GetFromJsonAsync<JsonElement>($"/api/circles/{circleId}/succession", Json);
        Assert.Equal("open", stillOpen.GetProperty("status").GetString());

        var first = a.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = aAuth.UserId });
        var second = b.PostAsJsonAsync($"/api/circles/{circleId}/succession/votes", new { candidateUserId = aAuth.UserId });
        await Task.WhenAll(first, second);
        Assert.Equal(HttpStatusCode.NoContent, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await second).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(item => item.Id == circleId);
            Assert.Equal(aAuth.UserId, stored.OwnerUserId);
            Assert.Equal(CircleStatus.Active, stored.Status);
        }

        var lockedDono = _factory.CreateClient();
        var lockedAuth = await AuthHelper.RegisterAsync(lockedDono, name: "Outro");
        lockedDono.WithToken(lockedAuth.Token);
        var viewer = _factory.CreateClient();
        var viewerAuth = await AuthHelper.RegisterAsync(viewer, name: "Viewer");
        viewer.WithToken(viewerAuth.Token);
        var segundo = _factory.CreateClient();
        var segundoAuth = await AuthHelper.RegisterAsync(segundo, name: "Segundo");
        segundo.WithToken(segundoAuth.Token);
        var other = await (await lockedDono.PostAsJsonAsync("/api/circles", new { name = "Sem maioria", visibility = "public" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var otherId = other.GetProperty("id").GetGuid();
        var invite = await (await lockedDono.PostAsJsonAsync($"/api/circles/{otherId}/invites", new { email = viewerAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var invite2 = await (await lockedDono.PostAsJsonAsync($"/api/circles/{otherId}/invites", new { email = segundoAuth.Email, role = "viewer" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        (await viewer.PostAsJsonAsync($"/api/circles/invites/{invite.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();
        (await segundo.PostAsJsonAsync($"/api/circles/invites/{invite2.GetProperty("token").GetString()}/accept", new { })).EnsureSuccessStatusCode();
        (await lockedDono.DeleteAsync($"/api/circles/{otherId}/membership")).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var open = await db.SuccessionVotes.SingleAsync(item => item.CircleId == otherId);
            open.DeadlineAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var expired = await viewer.GetFromJsonAsync<JsonElement>($"/api/circles/{otherId}/succession", Json);
        Assert.Equal("expired", expired.GetProperty("status").GetString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Circles.SingleAsync(item => item.Id == otherId);
            Assert.Equal(CircleStatus.Active, stored.Status);
            Assert.Null(stored.OwnerUserId);
            Assert.NotNull(stored.OwnerLostAt);
        }
    }

    [Fact]
    public async Task ExclusaoDefinitiva_NaoDesfaz_EConfirmacaoObrigatoria()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var created = await (await client.PostAsJsonAsync("/api/circles", new { name = "Apagar" })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/circles/{id}/delete", new { confirmation = "apagar" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/circles/{id}/delete", new { confirmation = "excluir definitivo" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/circles/{id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/circles/{id}/delete", new { confirmation = "excluir definitivo" })).StatusCode);
    }

    [Fact]
    public async Task BuscaPorRaio_SoComPermissaoExplicita_SemCoordenada()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Perto");
        dono.WithToken(donoAuth.Token);
        var outro = _factory.CreateClient();
        outro.WithToken((await AuthHelper.RegisterAsync(outro, name: "Busca")).Token);

        (await dono.PutAsJsonAsync("/api/profile", new
        {
            displayName = "Perto",
            findableByLocation = true,
            latitude = -23.55,
            longitude = -46.63,
            bio = "Moro no centro",
        })).EnsureSuccessStatusCode();
        var circle = await (await dono.PostAsJsonAsync("/api/circles", new
        {
            name = "Corrida",
            description = "Grupo da manhã",
            findableByLocation = true,
            latitude = -23.55,
            longitude = -46.63,
        })).Content.ReadFromJsonAsync<JsonElement>(Json);

        var hits = await outro.GetFromJsonAsync<List<JsonElement>>(
            "/api/search/nearby?lat=-23.56&lng=-46.64&radiusKm=20",
            Json);
        Assert.DoesNotContain(hits!, item => item.GetProperty("kind").GetString() == "person");
        Assert.Contains(hits!, item => item.GetProperty("kind").GetString() == "circle" && item.GetProperty("name").GetString() == "Corrida");
        foreach (var hit in hits!)
        {
            Assert.False(hit.TryGetProperty("latitude", out _));
            Assert.False(hit.TryGetProperty("longitude", out _));
            Assert.True(hit.GetProperty("distanceKm").GetDouble() >= 0);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await db.Profiles.SingleAsync(item => item.UserId == donoAuth.UserId);
            Assert.Null(profile.Latitude);
            Assert.Null(profile.Longitude);
            Assert.False(await db.SearchQueries.AnyAsync(item => item.Term.Contains("23.56") || item.Term.Contains("46.64")));
            Assert.False(await db.CircleLogs.AnyAsync(item => item.Detail.Contains("23.55") || item.Detail.Contains("46.63")));
        }

        (await dono.PutAsJsonAsync("/api/profile", new { displayName = "Perto", findableByLocation = false })).EnsureSuccessStatusCode();
        (await dono.PutAsJsonAsync($"/api/circles/{circle.GetProperty("id").GetGuid()}/location", new { findableByLocation = false })).EnsureSuccessStatusCode();
        var empty = await outro.GetFromJsonAsync<List<JsonElement>>(
            "/api/search/nearby?lat=-23.56&lng=-46.64&radiusKm=20",
            Json);
        Assert.DoesNotContain(empty!, item => item.GetProperty("name").GetString() is "Perto" or "Corrida");
    }

    [Fact]
    public async Task Geo_ArredondaKm()
    {
        Assert.Equal(0, GeoDistance.RoundedKm(GeoDistance.Kilometers(-23.55, -46.63, -23.55, -46.63)));
        Assert.True(GeoDistance.Kilometers(-23.55, -46.63, -23.56, -46.64) > 0);
    }
}
