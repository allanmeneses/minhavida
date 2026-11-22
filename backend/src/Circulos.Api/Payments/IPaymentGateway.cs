namespace Circulos.Api.Payments;

public enum ProviderSubscriptionStatus
{
    Pending,
    Authorized,
    Paused,
    Cancelled,
    Unknown,
}

public sealed record SubscriptionRequest(
    string ExternalReference,
    string PayerEmail,
    decimal Amount,
    string Reason,
    string BackUrl,
    string? NotificationUrl,
    string CardTokenId,
    string FrequencyType,
    int Frequency);

public sealed record ProviderSubscription(
    string ProviderSubscriptionId,
    ProviderSubscriptionStatus Status,
    string? ExternalReference,
    string? Reason = null);

public class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner)
{
    public bool RateLimited { get; init; }
}

public interface IPaymentGateway
{
    string Name { get; }
    Task<ProviderSubscription> CreateSubscriptionAsync(SubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<ProviderSubscription?> GetSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);
    Task CancelSubscriptionAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);
    bool IsValidWebhookSignature(string? signatureHeader, string? requestId, string dataId);
}
