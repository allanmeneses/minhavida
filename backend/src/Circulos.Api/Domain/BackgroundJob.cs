namespace Circulos.Api.Domain;

public sealed class BackgroundJob
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime RunAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
