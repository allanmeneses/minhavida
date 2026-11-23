namespace Circulos.Api.Domain;

public sealed class CircleInvite
{
    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsOpenLink { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public CircleRole Role { get; set; } = CircleRole.Viewer;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Circle Circle { get; set; } = null!;
}
