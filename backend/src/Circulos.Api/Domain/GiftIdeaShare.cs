namespace Circulos.Api.Domain;

public sealed class GiftIdeaShare
{
    public Guid Id { get; set; }
    public Guid ReminderId { get; set; }
    public Guid CircleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Reminder Reminder { get; set; } = null!;
    public Circle Circle { get; set; } = null!;
}
