namespace Circulos.Api.Domain;

public sealed class OnboardingExample
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? CircleId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DiscardedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
