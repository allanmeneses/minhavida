namespace Circulos.Api.Domain;

public sealed class CircleMember
{
    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public Guid UserId { get; set; }
    public CircleRole Role { get; set; }
    public DateTime JoinedAt { get; set; }

    public Circle Circle { get; set; } = null!;
    public User User { get; set; } = null!;
}
