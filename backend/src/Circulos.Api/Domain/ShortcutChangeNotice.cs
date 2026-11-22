namespace Circulos.Api.Domain;

public sealed class ShortcutChangeNotice
{
    public Guid Id { get; set; }
    public Guid ShortcutId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Shortcut Shortcut { get; set; } = null!;
}
