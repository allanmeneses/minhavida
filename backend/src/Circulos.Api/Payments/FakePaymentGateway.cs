using System.Collections.Concurrent;

namespace Circulos.Api.Payments;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string Secret = "fake-webhook-secret";
    public const string DeclinedCardToken = "fake-cartao-recusado";
    public const string ApprovedCardToken = "fake-cartao-aprovado";

    private readonly ConcurrentDictionary<string, ProviderSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, ProviderPayment> _payments = new();
    private readonly ConcurrentDictionary<string, string> _idempotency = new();
    private long _seq = 1000;
    private int _subscriptionReads;
    private int _paymentReads;

    public string Name => PaymentsConfig.Simulated;
    public ConcurrentDictionary<string, SubscriptionRequest> SubscriptionRequests { get; } = new();
    public ConcurrentDictionary<string, PixPaymentRequest> PixRequests { get; } = new();
    public ConcurrentQueue<string> SubscriptionCancels { get; } = new();
    public Func<Task>? BeforeCreateSubscription { get; set; }
    public Func<Task>? BeforeGetSubscription { get; set; }
    public Func<Task>? BeforeGetPayment { get; set; }
    public int SubscriptionReads => _subscriptionReads;
    public int PaymentReads => _paymentReads;

    public async Task<ProviderSubscription> CreateSubscriptionAsync(SubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (BeforeCreateSubscription is { } hook)
        {
            await hook();
        }

        if (request.CardTokenId == DeclinedCardToken)
        {
            throw new PaymentGatewayException("Cartao recusado (simulado).");
        }

        SubscriptionRequests[request.ExternalReference] = request;
        var id = "sub-" + Interlocked.Increment(ref _seq);
        var sub = new ProviderSubscription(id, ProviderSubscriptionStatus.Authorized, request.ExternalReference, request.Reason);
        _subscriptions[id] = sub;
        return sub;
    }

    public async Task<ProviderSubscription?> GetSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _subscriptionReads);
        if (BeforeGetSubscription is { } hook)
        {
            await hook();
        }

        return _subscriptions.TryGetValue(providerSubscriptionId, out var sub) ? sub : null;
    }

    public Task CancelSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
        SubscriptionCancels.Enqueue(providerSubscriptionId);
        if (_subscriptions.TryGetValue(providerSubscriptionId, out var sub))
        {
            _subscriptions[providerSubscriptionId] = sub with { Status = ProviderSubscriptionStatus.Cancelled };
        }

        return Task.CompletedTask;
    }

    public Task<PixPaymentResult> CreatePixPaymentAsync(PixPaymentRequest request, CancellationToken cancellationToken = default)
    {
        PixRequests[request.ExternalReference] = request;
        var id = _idempotency.GetOrAdd(request.IdempotencyKey, _ => Interlocked.Increment(ref _seq).ToString());
        _payments.TryAdd(id, new ProviderPayment(id, ProviderPaymentStatus.Pending, request.Amount, request.ExternalReference, null));
        var code = "00020126580014br.gov.bcb.pix0136circulos-fake-" + id;
        return Task.FromResult(new PixPaymentResult(id, code, null, null));
    }

    public async Task<ProviderPayment?> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _paymentReads);
        if (BeforeGetPayment is { } hook)
        {
            await hook();
        }

        return _payments.TryGetValue(providerPaymentId, out var payment) ? payment : null;
    }

    public void SetSubscriptionStatus(string id, ProviderSubscriptionStatus status)
    {
        if (_subscriptions.TryGetValue(id, out var sub))
        {
            _subscriptions[id] = sub with { Status = status };
        }
    }

    public ProviderPayment ApprovePayment(string providerPaymentId, DateTime? approvedAtUtc = null)
    {
        return _payments.AddOrUpdate(
            providerPaymentId,
            id => new ProviderPayment(id, ProviderPaymentStatus.Approved, 0, null, approvedAtUtc ?? DateTime.UtcNow),
            (_, payment) => payment with
            {
                Status = ProviderPaymentStatus.Approved,
                ApprovedAtUtc = approvedAtUtc ?? DateTime.UtcNow,
            });
    }

    public bool IsValidWebhookSignature(string? signatureHeader, string? requestId, string dataId) =>
        MercadoPagoGateway.VerifySignature(Secret, signatureHeader, requestId, dataId);
}
