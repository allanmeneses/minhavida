using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class ReminderTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ReminderTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Aniversario_ComAntecedenciaEListaDeIdeias()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var start = DateTime.UtcNow.Date.AddDays(10).AddHours(12);
        var response = await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Aniversario da Maria",
            kind = "birthday",
            startAt = start,
            personName = "Maria",
            advanceDays = 3,
            giftIdeas = new[] { "Livro", "Planta" },
            recurrence = new { frequency = "yearly", interval = 1 },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("birthday", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("advanceDays").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("giftIdeas").GetArrayLength());
    }

    [Fact]
    public async Task Pausa_MantemProximoDisparoDaGrade()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(9);
        var created = await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Remedio",
            startAt = start,
            recurrence = new { frequency = "daily", interval = 1 },
        });
        var reminder = await created.Content.ReadFromJsonAsync<ReminderDto>(Json);
        var original = reminder!.NextFireAt;

        var paused = await client.PostAsync($"/api/reminders/{reminder.Id}/pause", null);
        var pausedBody = await paused.Content.ReadFromJsonAsync<ReminderDto>(Json);
        Assert.True(pausedBody!.IsPaused);
        Assert.Equal(original, pausedBody.NextFireAt);

        var resumed = await client.PostAsync($"/api/reminders/{reminder.Id}/resume", null);
        var resumedBody = await resumed.Content.ReadFromJsonAsync<ReminderDto>(Json);
        Assert.False(resumedBody!.IsPaused);
        Assert.Equal(original, resumedBody.NextFireAt);
    }

    [Fact]
    public async Task Skip_AvancaParaAProximaOcorrencia()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(9);
        var created = await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Remedio",
            startAt = start,
            recurrence = new { frequency = "daily", interval = 1 },
        });
        var reminder = await created.Content.ReadFromJsonAsync<ReminderDto>(Json);
        var skipped = await client.PostAsync($"/api/reminders/{reminder!.Id}/skip", null);
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
        var body = await skipped.Content.ReadFromJsonAsync<ReminderDto>(Json);
        Assert.True(body!.NextFireAt > reminder.NextFireAt);
    }

    [Fact]
    public async Task RecorrenciaComPoucoRestante_MarcaEndingSoon()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var start = DateTime.UtcNow.AddHours(2);
        var created = await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Ultimas doses",
            startAt = start,
            recurrence = new { frequency = "daily", interval = 1, maxOccurrences = 2 },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("endingSoon").GetBoolean());
    }

    [Fact]
    public async Task ModoFerias_EscondeLembretesDoPainelEGuardaORegistro()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var start = DateTime.UtcNow.AddHours(-2);
        await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Atrasado",
            startAt = start,
            recurrence = new { frequency = "none" },
        });

        var before = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.True(before.GetProperty("overdue").GetArrayLength() >= 1);

        var vacation = await client.PutAsJsonAsync("/api/reminders/vacation", new { until = DateTime.UtcNow.AddDays(5) });
        Assert.Equal(HttpStatusCode.NoContent, vacation.StatusCode);

        var during = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.Equal(0, during.GetProperty("overdue").GetArrayLength());
        Assert.Equal(0, during.GetProperty("dueToday").GetArrayLength());
        Assert.NotEqual(JsonValueKind.Null, during.GetProperty("vacationUntil").ValueKind);

        var list = await client.GetFromJsonAsync<List<ReminderDto>>("/api/reminders", Json);
        Assert.Contains(list!, x => x.Title == "Atrasado");
    }

    [Fact]
    public async Task VoltaDasFerias_IgnoraOneshotQueJaNaoFazSentido()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var start = DateTime.UtcNow.AddDays(-10);
        await client.PostAsJsonAsync("/api/reminders", new
        {
            title = "Passado",
            startAt = start,
            recurrence = new { frequency = "none" },
        });
        await client.PutAsJsonAsync("/api/reminders/vacation", new { until = DateTime.UtcNow.AddDays(-1) });

        var home = await client.GetFromJsonAsync<JsonElement>("/api/home", Json);
        Assert.DoesNotContain(home.GetProperty("overdue").EnumerateArray(), x => x.GetProperty("title").GetString() == "Passado");
    }

    private sealed record ReminderDto(Guid Id, string Title, DateTime NextFireAt, bool IsPaused);
}
