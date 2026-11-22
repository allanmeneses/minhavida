namespace Circulos.Api.Domain;

public sealed class Reminder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? CircleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ReminderKind Kind { get; set; } = ReminderKind.General;
    public string RecurrenceJson { get; set; } = "{}";
    public DateTime StartAt { get; set; }
    public DateTime NextFireAt { get; set; }
    public DateTime? UntilAt { get; set; }
    public int? MaxOccurrences { get; set; }
    public int CompletedCount { get; set; }
    public bool IsPaused { get; set; }
    public bool SkipNext { get; set; }
    public string? PersonName { get; set; }
    public int AdvanceDays { get; set; }
    public string GiftIdeasJson { get; set; } = "[]";
    public string? PhotoStorageKey { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
