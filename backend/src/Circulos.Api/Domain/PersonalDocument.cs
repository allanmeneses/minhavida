namespace Circulos.Api.Domain;

public sealed class PersonalDocument
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
