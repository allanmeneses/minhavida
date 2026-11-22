namespace Circulos.Api.Domain;

public sealed class Shortcut
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ReminderId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#1F6F66";
    public ShortcutActionKind ActionKind { get; set; } = ShortcutActionKind.MarkDone;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
