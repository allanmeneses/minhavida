using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Services;

public sealed class BillingService(
    AppDbContext db,
    IPaymentGateway gateway,
    IEmailSender email,
    TimeProvider clock,
    IOptions<BillingOptions> billingOptions,
    IOptions<PlanPriceOptions> prices,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    public bool Enabled =>
        billingOptions.Value.Enabled
        || environment.IsEnvironment("Testing");

    public bool Sandbox => billingOptions.Value.Sandbox;
    public string ProviderName => gateway.Name;

    public PlanCatalogResponse Catalog()
    {
        var monthly = prices.Value.PaidMonthlyReais;
        var yearly = prices.Value.PaidYearlyReais;
        return new PlanCatalogResponse(
            "PROPOSTA",
            new PlanOfferResponse(
                "free",
                "Gratuito",
                0,
                PlanLimits.FreeCircleLimit,
                PlanLimits.FreeStorageBytes,
                PlanLimits.FreeShortcutLimit,
                "Limites do plano gratuito da especificacao."),
            new PlanOfferResponse(
                "paid-monthly",
                "Pago mensal",
                monthly,
                null,
                PlanLimits.PaidStorageBytes,
                PlanLimits.PaidShortcutLimit,
                "Circulos ilimitados, 1 GB e mais atalhos. Valor em reais, configuravel."),
            new PlanOfferResponse(
                "paid-yearly",
                "Pago anual",
                yearly,
                null,
                PlanLimits.PaidStorageBytes,
                PlanLimits.PaidShortcutLimit,
                "Mesmos limites do pago, cobranca anual. Valor em reais, configuravel."),
            Sandbox,
            ProviderName);
    }

    public PlanResponse ToPlan(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var paid = PlanLimits.BenefitsActive(user, now);
        var limit = PlanLimits.StorageLimit(paid);
        var standing = paid;
        var checkout = Enabled
            ? (standing ? "Assinatura ativa." : "Assine com cartao recorrente no Mercado Pago (sandbox por padrao).")
            : "Cobranca recorrente ainda nao esta ligada a um gateway.";
        return new PlanResponse(
            paid ? "paid" : "free",
            PlanLimits.CircleLimit(paid),
            user.OwnedCircleCount,
            limit,
            user.StorageUsedBytes,
            PlanLimits.ShouldWarn(user.StorageUsedBytes, limit),
            checkout,
            EnumText.Subscription(user.SubscriptionStatus),
            standing,
            PlanLimits.ShortcutLimit(paid),
            Sandbox,
            ProviderName,
            user.SubscriptionProviderId);
    }

    public async Task<ActionResultLike<PlanResponse>> SubscribeAsync(User user, string? period, string? cardToken, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return ActionResultLike<PlanResponse>.Fail(StatusCodes.Status501NotImplemented, "Cobranca recorrente ainda nao esta ligada a um gateway.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (PlanLimits.BenefitsActive(user, now))
        {
            return ActionResultLike<PlanResponse>.Ok(ToPlan(user));
        }

        var yearly = string.Equals(period, "yearly", StringComparison.OrdinalIgnoreCase);
        var amount = yearly ? prices.Value.PaidYearlyReais : prices.Value.PaidMonthlyReais;
        var token = string.IsNullOrWhiteSpace(cardToken) ? FakePaymentGateway.ApprovedCardToken : cardToken.Trim();
        var publicBase = (configuration["PublicBaseUrl"] ?? "https://circulos.local").TrimEnd('/');
        var notification = publicBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? $"{publicBase}/webhooks/mercadopago?source_news=webhooks"
            : null;

        ProviderSubscription created;
        try
        {
            created = await gateway.CreateSubscriptionAsync(
                new SubscriptionRequest(
                    user.Id.ToString("N"),
                    user.Email,
                    amount,
                    yearly ? "Circulos pago anual" : "Circulos pago mensal",
                    $"{publicBase}/pricing",
                    notification,
                    token,
                    yearly ? "years" : "months",
                    1),
                cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            return ActionResultLike<PlanResponse>.Fail(StatusCodes.Status400BadRequest, ex.Message);
        }

        ApplyAuthorized(user, created, yearly, now);
        await db.SaveChangesAsync(cancellationToken);
        return ActionResultLike<PlanResponse>.Ok(ToPlan(user));
    }

    public async Task<ActionResultLike<PlanResponse>> CancelAsync(User user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.SubscriptionProviderId))
        {
            return ActionResultLike<PlanResponse>.Fail(StatusCodes.Status400BadRequest, "Nao ha assinatura para cancelar.");
        }

        await gateway.CancelSubscriptionAsync(user.SubscriptionProviderId, cancellationToken);
        user.SubscriptionStatus = SubscriptionStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        await email.SendAsync(
            user.Email,
            "Assinatura do Circulos cancelada",
            "Sua assinatura foi cancelada. O acesso pago segue ate o fim do ciclo. Este e-mail nao reativa a conta.",
            cancellationToken);
        return ActionResultLike<PlanResponse>.Ok(ToPlan(user));
    }

    public bool SignatureValid(string? signatureHeader, string? requestId, string dataId) =>
        gateway.IsValidWebhookSignature(signatureHeader, requestId, dataId);

    public async Task<string> ApplyWebhookAsync(string topic, string resourceId, CancellationToken cancellationToken)
    {
        var existing = await db.BillingEvents.FirstOrDefaultAsync(
            item => item.Provider == gateway.Name && item.EventKey == $"{topic}:{resourceId}",
            cancellationToken);
        if (existing is not null)
        {
            return "duplicate";
        }

        var remote = await gateway.GetSubscriptionAsync(resourceId, cancellationToken);
        if (remote is null)
        {
            db.BillingEvents.Add(Event(topic, resourceId, "not_found"));
            await db.SaveChangesAsync(cancellationToken);
            return "not_found";
        }

        if (!Guid.TryParseExact(remote.ExternalReference, "N", out var userId)
            && !Guid.TryParse(remote.ExternalReference, out userId))
        {
            db.BillingEvents.Add(Event(topic, resourceId, "reference_mismatch"));
            await db.SaveChangesAsync(cancellationToken);
            return "reference_mismatch";
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            db.BillingEvents.Add(Event(topic, resourceId, "user_not_found"));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "user_not_found";
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var result = ApplyRemote(user, remote, now);
        db.BillingEvents.Add(Event(topic, resourceId, result));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static string ApplyRemote(User user, ProviderSubscription remote, DateTime now)
    {
        user.SubscriptionProviderId = remote.ProviderSubscriptionId;
        switch (remote.Status)
        {
            case ProviderSubscriptionStatus.Authorized:
                user.Plan = PlanKind.Paid;
                user.SubscriptionStatus = SubscriptionStatus.Active;
                user.PaidUntil ??= now.AddMonths(1);
                return "active";
            case ProviderSubscriptionStatus.Paused:
                user.SubscriptionStatus = SubscriptionStatus.Suspended;
                return "suspended";
            case ProviderSubscriptionStatus.Cancelled:
                user.SubscriptionStatus = SubscriptionStatus.Cancelled;
                return "cancelled";
            case ProviderSubscriptionStatus.Pending:
                user.SubscriptionStatus = SubscriptionStatus.PastDue;
                return "past_due";
            default:
                user.SubscriptionStatus = SubscriptionStatus.Suspended;
                return "suspended";
        }
    }

    private void ApplyAuthorized(User user, ProviderSubscription created, bool yearly, DateTime now)
    {
        user.Plan = PlanKind.Paid;
        user.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionProviderId = created.ProviderSubscriptionId;
        user.BillingPeriod = yearly ? BillingPeriod.Yearly : BillingPeriod.Monthly;
        user.PaidUntil = yearly ? now.AddYears(1) : now.AddMonths(1);
    }

    private BillingEvent Event(string topic, string resourceId, string result) =>
        new()
        {
            Id = Guid.NewGuid(),
            Provider = gateway.Name,
            EventKey = $"{topic}:{resourceId}",
            Topic = topic,
            ResourceId = resourceId,
            Result = result,
            ReceivedAt = clock.GetUtcNow().UtcDateTime,
        };
}

public sealed class ActionResultLike<T>
{
    public int Status { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static ActionResultLike<T> Ok(T value) => new() { Status = StatusCodes.Status200OK, Value = value };
    public static ActionResultLike<T> Fail(int status, string error) => new() { Status = status, Error = error };
}
