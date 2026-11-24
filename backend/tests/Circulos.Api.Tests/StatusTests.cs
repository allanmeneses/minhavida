using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Circulos.Api.Observability;
using Microsoft.Extensions.Configuration;

namespace Circulos.Api.Tests;

public sealed class StatusTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public StatusTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthEStatus_PublicosEBancoOk()
    {
        var client = _factory.CreateClient();
        var live = await client.GetFromJsonAsync<JsonElement>("/health", Json);
        Assert.Equal("ok", live.GetProperty("status").GetString());

        var ready = await client.GetFromJsonAsync<JsonElement>("/health/ready", Json);
        Assert.Equal("ok", ready.GetProperty("database").GetString());
        Assert.Equal("off", ready.GetProperty("telemetry").GetString());

        var json = await client.GetFromJsonAsync<JsonElement>("/api/status", Json);
        Assert.Equal("ok", json.GetProperty("api").GetString());

        var page = await client.GetAsync("/status");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("text/html", page.Content.Headers.ContentType?.ToString());
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Status do Círculos", html);
        Assert.Contains("Banco", html);
        Assert.Contains("Telemetria", html);
        Assert.DoesNotContain("GET /health", html);
        Assert.DoesNotContain("OpenTelemetry", html);
        Assert.Matches(@"\d{2}/\d{2}/\d{4} \d{2}:\d{2}", html);
    }

    [Fact]
    public void OpenTelemetry_DesligadoPorPadrao_LigaPorVariavel()
    {
        var off = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = "false",
        }).Build();
        Assert.False(ObservabilitySetup.IsEnabled(off));

        var on = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_ENABLED"] = "true",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317",
        }).Build();
        Assert.True(ObservabilitySetup.IsEnabled(on));
    }
}
