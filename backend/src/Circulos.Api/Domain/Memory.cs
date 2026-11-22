namespace Circulos.Api.Domain;

public sealed class Memory
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? CircleId { get; set; }
    public Guid? SourceCircleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateOnly? OccurredOn { get; set; }
    public string? PhotoStorageKey { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Author { get; set; } = null!;
    public Circle? Circle { get; set; }
}
