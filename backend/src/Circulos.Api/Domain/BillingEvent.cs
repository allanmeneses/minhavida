namespace Circulos.Api.Domain;

public sealed class BillingEvent
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "MercadoPago";
    public string EventKey { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
