namespace Circulos.Api.Domain;

public sealed class LifeEntryShare
{
    public Guid Id { get; set; }
    public Guid LifeEntryId { get; set; }
    public Guid CircleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public LifeEntry LifeEntry { get; set; } = null!;
    public Circle Circle { get; set; } = null!;
}
