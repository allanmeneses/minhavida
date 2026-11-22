namespace Circulos.Api.Domain;

public sealed class SearchQuery
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Term { get; set; } = string.Empty;
    public int TimesUsed { get; set; }
    public DateTime LastUsedAt { get; set; }

    public User User { get; set; } = null!;
}
