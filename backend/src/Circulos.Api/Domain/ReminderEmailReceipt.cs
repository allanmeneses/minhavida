namespace Circulos.Api.Domain;

public sealed class ReminderEmailReceipt
{
    public Guid Id { get; set; }
    public Guid ReminderId { get; set; }
    public Guid UserId { get; set; }
    public DateTime FireAt { get; set; }
    public DateTime SentAt { get; set; }

    public Reminder Reminder { get; set; } = null!;
    public User User { get; set; } = null!;
}
