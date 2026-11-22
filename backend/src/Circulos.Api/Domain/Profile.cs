namespace Circulos.Api.Domain;

public sealed class Profile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Phone { get; set; }
    public string? SecondaryEmail { get; set; }
    public bool IsPublic { get; set; }
    public bool FindableByLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PhotoStorageKey { get; set; }
    public string? PhotoContentType { get; set; }
    public string? PhotoFileName { get; set; }
    public long PhotoSizeBytes { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
