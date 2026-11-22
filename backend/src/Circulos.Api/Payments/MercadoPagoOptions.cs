namespace Circulos.Api.Payments;

public sealed class MercadoPagoOptions
{
    public string BaseUrl { get; set; } = "https://api.mercadopago.com";
    public string? AccessToken { get; set; }
    public string? WebhookSecret { get; set; }
    public string? PublicKey { get; set; }
}

public sealed class BillingOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Simulated";
    public bool Sandbox { get; set; } = true;
}

public sealed class PlanPriceOptions
{
    public decimal PaidMonthlyReais { get; set; } = 29.90m;
    public decimal PaidYearlyReais { get; set; } = 299.00m;
}
