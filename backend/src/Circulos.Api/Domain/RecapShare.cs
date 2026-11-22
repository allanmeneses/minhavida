namespace Circulos.Api.Domain;

public sealed class RecapShare
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CircleId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Circle Circle { get; set; } = null!;
}
