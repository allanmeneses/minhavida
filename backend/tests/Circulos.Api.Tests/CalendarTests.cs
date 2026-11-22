using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class CalendarTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public CalendarTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Mes_MostraOcorrenciasEExportaIcsComAssinatura()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var start = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        (await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Tomei o remedio",
            startAt = start,
            recurrence = new { frequency = "daily", interval = 1 },
        })).EnsureSuccessStatusCode();

        var month = await client.GetFromJsonAsync<JsonElement>("/api/calendar?from=2026-11-01&to=2026-11-07", Json);
        Assert.True(month.GetProperty("events").GetArrayLength() >= 7);
        Assert.Contains(month.GetProperty("events").EnumerateArray(), x => x.GetProperty("title").GetString() == "Tomei o remedio");
        var feedUrl = month.GetProperty("feedUrl").GetString();
        Assert.StartsWith("/api/calendar/feed/", feedUrl);

        var ics = await client.GetAsync("/api/calendar/ics?from=2026-11-01&to=2026-11-07");
        Assert.Equal(HttpStatusCode.OK, ics.StatusCode);
        Assert.Equal("text/calendar; charset=utf-8", ics.Content.Headers.ContentType?.ToString());
        var body = await ics.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VCALENDAR", body);
        Assert.Contains("Tomei o remedio", body);

        var token = feedUrl!["/api/calendar/feed/".Length..];
        var anonymous = _factory.CreateClient();
        var feed = await anonymous.GetAsync($"/api/calendar/feed/{token}");
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.Contains("BEGIN:VEVENT", await feed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Skip_NaoMarcaAOcorrenciaPuladaNoCalendario()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var start = DateTime.UtcNow.Date.AddHours(8);
        var created = await (await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Pular um dia",
            startAt = start,
            recurrence = new { frequency = "daily", interval = 1 },
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var next = created.GetProperty("nextFireAt").GetDateTime();
        (await client.PostAsync($"/api/reminders/{created.GetProperty("id").GetGuid()}/skip", null)).EnsureSuccessStatusCode();
        var from = DateOnly.FromDateTime(next.AddDays(-1));
        var to = DateOnly.FromDateTime(next.AddDays(1));
        var month = await client.GetFromJsonAsync<JsonElement>($"/api/calendar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", Json);
        Assert.DoesNotContain(
            month.GetProperty("events").EnumerateArray(),
            x => DateTime.Parse(x.GetProperty("at").GetString()!).ToUniversalTime() == DateTime.SpecifyKind(next, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Feed_PrimeiroAcessoSobreposto_GeraUmTokenSo()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(a.GetAsync("/api/calendar"), b.GetAsync("/api/calendar"));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
        Assert.False(string.IsNullOrWhiteSpace(user.CalendarFeedToken));
    }
}

public sealed class IcsCalendarTests
{
    [Fact]
    public void MontaEventoComResumoEEscapaTexto()
    {
        var ics = IcsCalendar.Build("Lembretes",
        [
            new IcsEvent("uid-1", new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc), "Cafe; da manha", "Linha\n2"),
        ]);
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("SUMMARY:Cafe\\; da manha", ics);
        Assert.Contains("DESCRIPTION:Linha\\n2", ics);
        Assert.Contains("\r\n", ics);
    }
}
