namespace Circulos.Api.Domain;

public sealed class AccountAppeal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public User User { get; set; } = null!;
}
