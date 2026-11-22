namespace Circulos.Api.Domain;

public sealed class LifeEntry
{
    public Guid Id { get; set; }
    public Guid LifeAreaId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public decimal? NumberValue { get; set; }
    public DateOnly? OccurredOn { get; set; }
    public string? Location { get; set; }
    public string? PhotoStorageKey { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSizeBytes { get; set; }
    public string TagsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public LifeArea LifeArea { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<LifeEntryShare> Shares { get; set; } = new List<LifeEntryShare>();
}
