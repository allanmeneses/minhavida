using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Circulos.Api.Tests;

public sealed class MercadoPagoApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("PAYMENTS_PROVIDER", "MercadoPago");
        builder.UseSetting("Payments:Provider", "MercadoPago");
        builder.UseSetting("MercadoPago:AccessToken", "TEST-não-e-credencial-real");
        builder.UseSetting("MercadoPago:WebhookSecret", "webhook-secret-teste");
    }
}

public sealed class MercadoPagoProviderTests : IClassFixture<MercadoPagoApiFactory>
{
    private readonly MercadoPagoApiFactory _factory;

    public MercadoPagoProviderTests(MercadoPagoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevRotina_404QuandoOProvedorEMercadoPago()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dev/rotina")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/dev/rotina", null)).StatusCode);
    }

    [Fact]
    public async Task Webhook_AssinaturaInvalida_401SemChamarOMercadoPago()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/mercadopago")
        {
            Content = new StringContent("""{"type":"subscription_preapproval","data":{"id":"pre-1"}}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("x-signature", "ts=1,v1=00");
        request.Headers.TryAddWithoutValidation("x-request-id", "req-1");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
