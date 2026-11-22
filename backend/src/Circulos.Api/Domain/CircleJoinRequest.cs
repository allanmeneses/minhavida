namespace Circulos.Api.Domain;

public sealed class CircleJoinRequest
{
    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }

    public Circle Circle { get; set; } = null!;
    public User User { get; set; } = null!;
}
