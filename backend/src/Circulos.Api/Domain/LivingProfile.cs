namespace Circulos.Api.Domain;

public enum LivingProfileKind
{
    Person = 0,
    Pet = 1,
    Group = 2,
}

public sealed class LivingProfile
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? CircleId { get; set; }
    public LivingProfileKind Kind { get; set; } = LivingProfileKind.Person;
    public string Name { get; set; } = string.Empty;
    public string? Context { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? PhotoStorageKey { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Circle? Circle { get; set; }
}
