namespace Circulos.Api.Domain;

public sealed class CircleLog
{
    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Circle Circle { get; set; } = null!;
}
