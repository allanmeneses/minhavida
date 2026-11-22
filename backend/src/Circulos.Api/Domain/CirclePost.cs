namespace Circulos.Api.Domain;

public sealed class CirclePost
{
    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? PhotoStorageKey { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Circle Circle { get; set; } = null!;
    public User? Author { get; set; }
}
