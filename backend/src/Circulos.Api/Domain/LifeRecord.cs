namespace Circulos.Api.Domain;

public sealed class LifeRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateOnly? OccurredOn { get; set; }
    public decimal? NumberValue { get; set; }
    public string FieldsJson { get; set; } = "{}";
    public string? PhotoStorageKey { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSizeBytes { get; set; }
    public string? FileStorageKey { get; set; }
    public string? FileContentType { get; set; }
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public Guid? ReminderId { get; set; }
    public Guid? OriginLifeEntryId { get; set; }
    public Guid? OriginDiaryPageId { get; set; }
    public Guid? OriginDocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public RecordType Type { get; set; } = null!;
    public Reminder? Reminder { get; set; }
    public ICollection<RecordShare> Shares { get; set; } = new List<RecordShare>();
}
