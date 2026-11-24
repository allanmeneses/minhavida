namespace Circulos.Api.Domain;

public sealed class RecordType
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";
    public Guid? OriginTemplateId { get; set; }
    public Guid? OriginLifeAreaId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsGallery => UserId is null;

    public User? User { get; set; }
    public ICollection<LifeRecord> Records { get; set; } = new List<LifeRecord>();
}
