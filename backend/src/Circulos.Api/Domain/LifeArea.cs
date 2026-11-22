namespace Circulos.Api.Domain;

public sealed class LifeArea
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public LifeAreaKind Kind { get; set; }
    public int SortOrder { get; set; }
    public bool Suggested { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<LifeEntry> Entries { get; set; } = new List<LifeEntry>();
}
