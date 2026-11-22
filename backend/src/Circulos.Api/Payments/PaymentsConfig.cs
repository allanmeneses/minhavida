namespace Circulos.Api.Payments;

public static class PaymentsConfig
{
    public const string Simulated = "Simulated";
    public const string MercadoPago = "MercadoPago";

    public static string Provider(IConfiguration config)
    {
        var raw = First(
            config["PAYMENTS_PROVIDER"],
            config["Payments:Provider"],
            config["Billing:Provider"]) ?? Simulated;
        if (raw.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("MP", StringComparison.OrdinalIgnoreCase))
        {
            return MercadoPago;
        }

        return Simulated;
    }

    public static bool IsSimulated(IConfiguration config) => Provider(config) == Simulated;

    public static bool IsMercadoPago(IConfiguration config) => Provider(config) == MercadoPago;

    public static string PublicBaseUrl(IConfiguration config) =>
        (First(config["PUBLIC_BASE_URL"], config["PublicBaseUrl"], config["Platform:PublicBaseUrl"]) ?? string.Empty)
            .TrimEnd('/');

    private static string? First(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
