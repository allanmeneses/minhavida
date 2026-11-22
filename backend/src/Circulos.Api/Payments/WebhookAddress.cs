namespace Circulos.Api.Payments;

public static class WebhookAddress
{
    public static string? For(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{baseUrl.TrimEnd('/')}/webhooks/mercadopago?source_news=webhooks";
    }
}
