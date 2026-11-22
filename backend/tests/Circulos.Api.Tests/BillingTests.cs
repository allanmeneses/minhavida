using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class BillingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public BillingTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Catalogo_MostraPropostaComPrecosConfiguraveis()
    {
        var client = _factory.CreateClient();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/plans", Json);
        Assert.Equal("PROPOSTA", catalog.GetProperty("status").GetString());
        Assert.Equal(0, catalog.GetProperty("free").GetProperty("priceReais").GetDecimal());
        Assert.Equal(5, catalog.GetProperty("free").GetProperty("shortcutLimit").GetInt32());
        Assert.Equal(20, catalog.GetProperty("paidMonthly").GetProperty("shortcutLimit").GetInt32());
        Assert.True(catalog.GetProperty("sandbox").GetBoolean());
        Assert.Equal("Simulated", catalog.GetProperty("provider").GetString());
        Assert.Equal(29.90m, catalog.GetProperty("paidMonthly").GetProperty("priceReais").GetDecimal());
    }

    [Fact]
    public async Task CheckoutSimulado_AssinaEWebhookValidaAssinatura()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var checkout = await client.PostAsJsonAsync("/api/plan/checkout", new { period = "monthly", cardToken = FakePaymentGateway.ApprovedCardToken });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var plan = await checkout.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("paid", plan.GetProperty("plan").GetString());
        Assert.Equal("active", plan.GetProperty("subscriptionStatus").GetString());
        Assert.True(plan.GetProperty("inGoodStanding").GetBoolean());

        string providerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == auth.UserId);
            providerId = user.SubscriptionProviderId!;
            Assert.False(string.IsNullOrWhiteSpace(providerId));
        }

        var requestId = Guid.NewGuid().ToString("N");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var v1 = MercadoPagoGateway.Sign(FakePaymentGateway.Secret, MercadoPagoGateway.BuildManifest(requestId, providerId, ts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhook(client, "nao", "00", requestId, ts)).StatusCode);
        var webhook = await PostWebhook(client, providerId, v1, requestId, ts);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Contains("active", await webhook.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CartaoRecusado_NaoAssina_ECheckoutSobrepostoFicaUmaAssinatura()
    {
        var authClient = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(authClient);
        var declined = _factory.CreateClient();
        declined.WithToken(auth.Token);
        var failed = await declined.PostAsJsonAsync("/api/plan/checkout", new { cardToken = FakePaymentGateway.DeclinedCardToken });
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var first = a.PostAsJsonAsync("/api/plan/checkout", new { cardToken = FakePaymentGateway.ApprovedCardToken });
        var second = b.PostAsJsonAsync("/api/plan/checkout", new { cardToken = FakePaymentGateway.ApprovedCardToken });
        await Task.WhenAll(first, second);
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second).StatusCode);

        var gateway = _factory.Services.GetRequiredService<FakePaymentGateway>();
        Assert.Equal(1, gateway.SubscriptionRequests.Count(item => item.Key == auth.UserId.ToString("N")));
    }

    [Fact]
    public async Task Inadimplencia_BloqueiaAtalhoExtraEWebhookPausado()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        (await client.PostAsJsonAsync("/api/plan/checkout", new { cardToken = FakePaymentGateway.ApprovedCardToken })).EnsureSuccessStatusCode();
        for (var i = 0; i < 6; i++)
        {
            (await client.PostAsJsonAsync("/api/shortcuts", new { label = $"Pago {i}" })).EnsureSuccessStatusCode();
        }

        string providerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == auth.UserId);
            providerId = user.SubscriptionProviderId!;
        }

        var gateway = _factory.Services.GetRequiredService<FakePaymentGateway>();
        gateway.SetSubscriptionStatus(providerId, ProviderSubscriptionStatus.Paused);
        var requestId = Guid.NewGuid().ToString("N");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var v1 = MercadoPagoGateway.Sign(FakePaymentGateway.Secret, MercadoPagoGateway.BuildManifest(requestId, providerId, ts));
        (await PostWebhook(client, providerId, v1, requestId, ts)).EnsureSuccessStatusCode();

        var extras = await client.GetFromJsonAsync<List<JsonElement>>("/api/shortcuts", Json);
        var extra = extras!.OrderBy(item => item.GetProperty("sortOrder").GetInt32()).Skip(PlanLimits.FreeShortcutLimit).First();
        Assert.Equal(
            HttpStatusCode.PaymentRequired,
            (await client.PostAsync($"/api/shortcuts/{extra.GetProperty("id").GetGuid()}/run", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/shortcuts", new { label = "Setimo" })).StatusCode);
    }

    private static Task<HttpResponseMessage> PostWebhook(
        HttpClient client,
        string dataId,
        string v1,
        string requestId,
        string ts)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/mercadopago")
        {
            Content = new StringContent(
                "{\"type\":\"subscription_preapproval\",\"data\":{\"id\":\"" + dataId + "\"}}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.TryAddWithoutValidation("x-signature", $"ts={ts},v1={v1}");
        request.Headers.TryAddWithoutValidation("x-request-id", requestId);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task AssinaturaHmac_ManifestoDoMercadoPago()
    {
        const string secret = "test-secret";
        const string dataId = "12345";
        const string requestId = "abc";
        const string ts = "1700000000";
        var v1 = MercadoPagoGateway.Sign(secret, MercadoPagoGateway.BuildManifest(requestId, dataId, ts));
        Assert.True(MercadoPagoGateway.VerifySignature(secret, $"ts={ts},v1={v1}", requestId, dataId));
        Assert.False(MercadoPagoGateway.VerifySignature(secret, $"ts={ts},v1={v1}", "outro", dataId));
        Assert.False(MercadoPagoGateway.VerifySignature(secret, $"ts={ts},v1=00", requestId, dataId));
    }
}
