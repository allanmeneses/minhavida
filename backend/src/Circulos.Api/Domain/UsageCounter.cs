namespace Circulos.Api.Domain;

public sealed class UsageCounter
{
    public string Metric { get; set; } = string.Empty;
    public long Count { get; set; }
}
