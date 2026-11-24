namespace Circulos.Api.Domain;

public sealed class RecordShare
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public Guid CircleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public LifeRecord Record { get; set; } = null!;
    public Circle Circle { get; set; } = null!;
}
