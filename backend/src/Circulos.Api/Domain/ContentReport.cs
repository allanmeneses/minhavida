namespace Circulos.Api.Domain;

public sealed class ContentReport
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public string TargetKind { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Reporter { get; set; } = null!;
}
