namespace Circulos.Api.Domain;

public sealed class ShortcutShare
{
    public Guid Id { get; set; }
    public Guid ShortcutId { get; set; }
    public Guid CircleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Shortcut Shortcut { get; set; } = null!;
    public Circle Circle { get; set; } = null!;
}
