using System.Collections.Concurrent;

namespace Circulos.Api.Payments;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string Secret = "fake-webhook-secret";
    public const string DeclinedCardToken = "fake-cartao-recusado";
    public const string ApprovedCardToken = "fake-cartao-aprovado";

    private readonly ConcurrentDictionary<string, ProviderSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, SubscriptionRequest> _requests = new();
    private long _seq = 1000;

    public string Name => "Simulated";
    public ConcurrentDictionary<string, SubscriptionRequest> SubscriptionRequests => _requests;
    public ConcurrentQueue<string> SubscriptionCancels { get; } = new();
    public Func<Task>? BeforeCreateSubscription { get; set; }
    public Func<Task>? BeforeGetSubscription { get; set; }

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

        _requests[request.ExternalReference] = request;
        var id = "sub-" + Interlocked.Increment(ref _seq);
        var sub = new ProviderSubscription(id, ProviderSubscriptionStatus.Authorized, request.ExternalReference, request.Reason);
        _subscriptions[id] = sub;
        return sub;
    }

    public async Task<ProviderSubscription?> GetSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
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

    public void SetSubscriptionStatus(string id, ProviderSubscriptionStatus status)
    {
        if (_subscriptions.TryGetValue(id, out var sub))
        {
            _subscriptions[id] = sub with { Status = status };
        }
    }

    public bool IsValidWebhookSignature(string? signatureHeader, string? requestId, string dataId) =>
        MercadoPagoGateway.VerifySignature(Secret, signatureHeader, requestId, dataId);
}
