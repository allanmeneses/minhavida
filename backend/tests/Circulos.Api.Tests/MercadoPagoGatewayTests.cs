using System.Net;
using System.Text;
using Circulos.Api.Payments;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Tests;

public sealed class MercadoPagoGatewayTests
{
    [Fact]
    public void WebhookAddress_SoEnviaNotificationComHttps()
    {
        Assert.Null(WebhookAddress.For(null));
        Assert.Null(WebhookAddress.For(""));
        Assert.Null(WebhookAddress.For("http://127.0.0.1:5288"));
        Assert.Equal(
            "https://circulos.example/webhooks/mercadopago?source_news=webhooks",
            WebhookAddress.For("https://circulos.example"));
    }

    [Fact]
    public async Task Preapproval_CriaConsultaECancela_NaApiDoMercadoPago()
    {
        var handler = new StubHandler
        {
            Respond = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Post && path == "/preapproval")
                {
                    return Json(HttpStatusCode.OK, """{"id":"pre-1","status":"authorized","external_reference":"abc","reason":"Circulos pago mensal"}""");
                }

                if (req.Method == HttpMethod.Get && path == "/preapproval/pre-1")
                {
                    return Json(HttpStatusCode.OK, """{"id":"pre-1","status":"authorized","external_reference":"abc"}""");
                }

                if (req.Method == HttpMethod.Put && path == "/preapproval/pre-1")
                {
                    return Json(HttpStatusCode.OK, """{"id":"pre-1","status":"cancelled","external_reference":"abc"}""");
                }

                return Json(HttpStatusCode.NotFound, "{}");
            },
        };
        var gateway = Gateway(handler);
        var created = await gateway.CreateSubscriptionAsync(new SubscriptionRequest(
            "abc",
            "pessoa@exemplo.com",
            29.90m,
            "Circulos pago mensal",
            "https://circulos.example/pricing",
            "https://circulos.example/webhooks/mercadopago?source_news=webhooks",
            "card-token",
            "months",
            1));
        Assert.Equal("pre-1", created.ProviderSubscriptionId);
        Assert.Equal(ProviderSubscriptionStatus.Authorized, created.Status);
        var post = handler.Bodies[0];
        Assert.Contains("\"external_reference\":\"abc\"", post, StringComparison.Ordinal);
        Assert.Contains("\"back_url\":\"https://circulos.example/pricing\"", post, StringComparison.Ordinal);
        Assert.Contains("notification_url", post, StringComparison.Ordinal);
        Assert.Contains("/preapproval", handler.Paths[0], StringComparison.Ordinal);

        var got = await gateway.GetSubscriptionAsync("pre-1");
        Assert.Equal("pre-1", got!.ProviderSubscriptionId);
        await gateway.CancelSubscriptionAsync("pre-1");
        Assert.Contains("/preapproval/pre-1", handler.Paths[2], StringComparison.Ordinal);
        Assert.Equal("PUT", handler.Methods[2]);
    }

    [Fact]
    public async Task Pix_CriaComIdempotencyKeyEConsultaPagamento()
    {
        var handler = new StubHandler
        {
            Respond = req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (req.Method == HttpMethod.Post && path == "/v1/payments")
                {
                    Assert.Equal("pix-1", req.Headers.GetValues("X-Idempotency-Key").Single());
                    return Json(
                        HttpStatusCode.OK,
                        """{"id":"pay-9","status":"pending","transaction_amount":29.9,"external_reference":"abc","point_of_interaction":{"transaction_data":{"qr_code":"pix-copia-cola"}}}""");
                }

                if (req.Method == HttpMethod.Get && path == "/v1/payments/pay-9")
                {
                    return Json(HttpStatusCode.OK, """{"id":"pay-9","status":"approved","transaction_amount":29.9,"external_reference":"abc","date_approved":"2026-11-22T12:00:00Z"}""");
                }

                return Json(HttpStatusCode.NotFound, "{}");
            },
        };
        var gateway = Gateway(handler);
        var created = await gateway.CreatePixPaymentAsync(new PixPaymentRequest(
            "abc",
            29.90m,
            "Circulos pago mensal",
            "Pessoa E2E",
            "pessoa@exemplo.com",
            DateTime.UtcNow.AddHours(2),
            "https://circulos.example/webhooks/mercadopago?source_news=webhooks",
            "pix-1"));
        Assert.Equal("pay-9", created.ProviderPaymentId);
        Assert.Equal("pix-copia-cola", created.QrCode);
        Assert.Contains("\"payment_method_id\":\"pix\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("notification_url", handler.Bodies[0], StringComparison.Ordinal);
        var got = await gateway.GetPaymentAsync("pay-9");
        Assert.Equal(ProviderPaymentStatus.Approved, got!.Status);
        Assert.Equal("abc", got.ExternalReference);
    }

    [Fact]
    public void AssinaturaHmac_ValidaERejeita()
    {
        const string secret = "test-secret";
        const string dataId = "12345";
        const string requestId = "abc";
        const string ts = "1700000000";
        var v1 = MercadoPagoGateway.Sign(secret, MercadoPagoGateway.BuildManifest(requestId, dataId, ts));
        Assert.True(MercadoPagoGateway.VerifySignature(secret, $"ts={ts},v1={v1}", requestId, dataId));
        Assert.False(MercadoPagoGateway.VerifySignature(secret, $"ts={ts},v1={v1}", "outro", dataId));
        Assert.False(MercadoPagoGateway.VerifySignature(secret, $"ts={ts},v1=00", requestId, dataId));
        Assert.False(MercadoPagoGateway.VerifySignature(secret, null, requestId, dataId));
    }

    private static MercadoPagoGateway Gateway(StubHandler handler)
    {
        var http = new HttpClient(handler);
        return new MercadoPagoGateway(
            http,
            Options.Create(new MercadoPagoOptions
            {
                BaseUrl = "https://api.mercadopago.com",
                AccessToken = "TEST-nao-e-credencial-real",
                WebhookSecret = "test-secret",
            }));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        public List<string> Paths { get; } = [];
        public List<string> Bodies { get; } = [];
        public required Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method.Method);
            Paths.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return Respond(request);
        }
    }
}
