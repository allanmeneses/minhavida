namespace Circulos.Api.Domain;

public sealed class AdminAction
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetKind { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User Admin { get; set; } = null!;
}
