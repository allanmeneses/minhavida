using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Payments;

public sealed class MercadoPagoGateway(HttpClient http, IOptions<MercadoPagoOptions> options) : IPaymentGateway
{
    private readonly MercadoPagoOptions _options = options.Value;
    private const int RateLimitAttempts = 3;
    private static readonly TimeSpan RateLimitWaitCap = TimeSpan.FromSeconds(2);
    internal Func<TimeSpan, CancellationToken, Task> WaitAsync { get; set; } = (delay, ct) => Task.Delay(delay, ct);

    public string Name => "MercadoPago";

    public async Task<ProviderSubscription> CreateSubscriptionAsync(SubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["reason"] = request.Reason,
            ["external_reference"] = request.ExternalReference,
            ["payer_email"] = request.PayerEmail,
            ["back_url"] = request.BackUrl,
            ["card_token_id"] = request.CardTokenId,
            ["status"] = "authorized",
            ["auto_recurring"] = new JsonObject
            {
                ["frequency"] = request.Frequency,
                ["frequency_type"] = request.FrequencyType,
                ["transaction_amount"] = request.Amount,
                ["currency_id"] = "BRL",
            },
        };
        if (!string.IsNullOrEmpty(request.NotificationUrl))
        {
            body["notification_url"] = request.NotificationUrl;
        }

        using var req = Request(HttpMethod.Post, "/preapproval", body);
        return ParseSubscription(await SendAsync(req, cancellationToken) ?? throw new PaymentGatewayException("Resposta do Mercado Pago vazia."));
    }

    public async Task<ProviderSubscription?> GetSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
        using var req = Request(HttpMethod.Get, $"/preapproval/{Uri.EscapeDataString(providerSubscriptionId)}");
        var json = await SendAsync(req, cancellationToken, allowNotFound: true);
        return json is null ? null : ParseSubscription(json);
    }

    public async Task CancelSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
        using var req = Request(HttpMethod.Put, $"/preapproval/{Uri.EscapeDataString(providerSubscriptionId)}", new JsonObject { ["status"] = "cancelled" });
        await SendAsync(req, cancellationToken);
    }

    public async Task<PixPaymentResult> CreatePixPaymentAsync(PixPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var (firstName, lastName) = SplitName(request.PayerName);
        var payer = new JsonObject
        {
            ["email"] = string.IsNullOrWhiteSpace(request.PayerEmail) ? request.ExternalReference + "@circulos.local" : request.PayerEmail,
            ["first_name"] = firstName,
            ["last_name"] = lastName,
        };
        var body = new JsonObject
        {
            ["transaction_amount"] = request.Amount,
            ["description"] = request.Description,
            ["payment_method_id"] = "pix",
            ["external_reference"] = request.ExternalReference,
            ["date_of_expiration"] = new DateTimeOffset(request.ExpiresAtUtc, TimeSpan.Zero)
                .ToOffset(TimeSpan.FromHours(-3))
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            ["payer"] = payer,
        };
        if (!string.IsNullOrEmpty(request.NotificationUrl))
        {
            body["notification_url"] = request.NotificationUrl;
        }

        using var req = Request(HttpMethod.Post, "/v1/payments", body);
        req.Headers.Add("X-Idempotency-Key", request.IdempotencyKey);
        var json = await SendAsync(req, cancellationToken) ?? throw new PaymentGatewayException("Resposta do Mercado Pago vazia.");
        var tx = json["point_of_interaction"]?["transaction_data"];
        return new PixPaymentResult(
            json["id"]?.ToString() ?? throw new PaymentGatewayException("Resposta do Mercado Pago sem id."),
            tx?["qr_code"]?.GetValue<string>(),
            tx?["qr_code_base64"]?.GetValue<string>(),
            tx?["ticket_url"]?.GetValue<string>());
    }

    public async Task<ProviderPayment?> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken = default)
    {
        using var req = Request(HttpMethod.Get, $"/v1/payments/{Uri.EscapeDataString(providerPaymentId)}");
        var json = await SendAsync(req, cancellationToken, allowNotFound: true);
        if (json is null)
        {
            return null;
        }

        var status = json["status"]?.GetValue<string>() switch
        {
            "approved" => ProviderPaymentStatus.Approved,
            "pending" or "in_process" or "authorized" => ProviderPaymentStatus.Pending,
            "cancelled" => ProviderPaymentStatus.Cancelled,
            "rejected" => ProviderPaymentStatus.Rejected,
            "refunded" or "charged_back" => ProviderPaymentStatus.Refunded,
            _ => ProviderPaymentStatus.Unknown,
        };
        DateTime? approved = DateTimeOffset.TryParse(
            json["date_approved"]?.GetValue<string>(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.UtcDateTime
            : null;
        return new ProviderPayment(
            json["id"]!.ToString(),
            status,
            json["transaction_amount"]?.GetValue<decimal>() ?? 0m,
            json["external_reference"]?.GetValue<string>(),
            approved);
    }

    public bool IsValidWebhookSignature(string? signatureHeader, string? requestId, string dataId) =>
        VerifySignature(_options.WebhookSecret, signatureHeader, requestId, dataId);

    public static bool VerifySignature(string? secret, string? signatureHeader, string? requestId, string dataId)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        string? ts = null;
        string? v1 = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            switch (kv[0].Trim())
            {
                case "ts":
                    ts = kv[1].Trim();
                    break;
                case "v1":
                    v1 = kv[1].Trim();
                    break;
            }
        }

        if (ts is null || v1 is null)
        {
            return false;
        }

        var expected = Sign(secret, BuildManifest(requestId, dataId, ts));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }

    public static string BuildManifest(string? requestId, string dataId, string ts)
    {
        var id = dataId.All(char.IsLetterOrDigit) ? dataId.ToLowerInvariant() : dataId;
        var sb = new StringBuilder();
        sb.Append("id:").Append(id).Append(';');
        if (!string.IsNullOrEmpty(requestId))
        {
            sb.Append("request-id:").Append(requestId).Append(';');
        }

        sb.Append("ts:").Append(ts).Append(';');
        return sb.ToString();
    }

    public static string Sign(string secret, string manifest) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

    private static ProviderSubscription ParseSubscription(JsonNode json)
    {
        var status = json["status"]?.GetValue<string>() switch
        {
            "authorized" => ProviderSubscriptionStatus.Authorized,
            "pending" => ProviderSubscriptionStatus.Pending,
            "paused" => ProviderSubscriptionStatus.Paused,
            "cancelled" or "canceled" => ProviderSubscriptionStatus.Cancelled,
            _ => ProviderSubscriptionStatus.Unknown,
        };
        return new ProviderSubscription(
            json["id"]?.ToString() ?? throw new PaymentGatewayException("Resposta do Mercado Pago sem id."),
            status,
            json["external_reference"]?.ToString(),
            json["reason"]?.GetValue<string>());
    }

    private HttpRequestMessage Request(HttpMethod method, string path, JsonNode? body = null)
    {
        var token = _options.AccessToken;
        if (string.IsNullOrEmpty(token))
        {
            throw new PaymentGatewayException("Mercado Pago sem access token configurado.");
        }

        var req = new HttpRequestMessage(method, new Uri(_options.BaseUrl.TrimEnd('/') + path));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        return req;
    }

    private async Task<JsonNode?> SendAsync(HttpRequestMessage req, CancellationToken cancellationToken, bool allowNotFound = false)
    {
        var snapshot = await SnapshotAsync(req, cancellationToken);
        for (var attempt = 1; ; attempt++)
        {
            using var message = snapshot.Create();
            HttpResponseMessage res;
            try
            {
                res = await http.SendAsync(message, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new PaymentGatewayException("Falha de comunicacao com o Mercado Pago.", ex);
            }

            using (res)
            {
                if (allowNotFound && res.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                var text = await res.Content.ReadAsStringAsync(cancellationToken);
                if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < RateLimitAttempts)
                {
                    await WaitAsync(TimeSpan.FromMilliseconds(500 * attempt) > RateLimitWaitCap ? RateLimitWaitCap : TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
                    continue;
                }

                if (!res.IsSuccessStatusCode)
                {
                    throw new PaymentGatewayException($"Mercado Pago respondeu {(int)res.StatusCode}: {(text.Length <= 400 ? text : text[..400])}")
                    {
                        RateLimited = res.StatusCode == System.Net.HttpStatusCode.TooManyRequests,
                    };
                }

                return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
            }
        }
    }

    private static async Task<RequestCopy> SnapshotAsync(HttpRequestMessage req, CancellationToken cancellationToken)
    {
        byte[]? body = null;
        MediaTypeHeaderValue? contentType = null;
        if (req.Content is not null)
        {
            body = await req.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = req.Content.Headers.ContentType;
        }

        var headers = new List<(string Name, string[] Values)>();
        foreach (var header in req.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            headers.Add((header.Key, header.Value.ToArray()));
        }

        return new RequestCopy(
            req.Method,
            req.RequestUri ?? throw new PaymentGatewayException("Requisicao do Mercado Pago sem endereco."),
            req.Headers.Authorization,
            headers,
            body,
            contentType);
    }

    private sealed record RequestCopy(
        HttpMethod Method,
        Uri Uri,
        AuthenticationHeaderValue? Authorization,
        List<(string Name, string[] Values)> Headers,
        byte[]? Body,
        MediaTypeHeaderValue? ContentType)
    {
        public HttpRequestMessage Create()
        {
            var req = new HttpRequestMessage(Method, Uri);
            if (Authorization is not null)
            {
                req.Headers.Authorization = Authorization;
            }

            foreach (var (name, values) in Headers)
            {
                req.Headers.TryAddWithoutValidation(name, values);
            }

            if (Body is not null)
            {
                var content = new ByteArrayContent(Body);
                if (ContentType is not null)
                {
                    content.Headers.ContentType = ContentType;
                }

                req.Content = content;
            }

            return req;
        }
    }

    private static (string First, string Last) SplitName(string name)
    {
        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("Pessoa", string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1]),
        };
    }
}
