namespace Circulos.Api.Domain;

public enum OwnershipOfferStatus
{
    Pending = 0,
    Accepted = 1,
    Cancelled = 2,
    Expired = 3,
}

public sealed class OwnershipOffer
{
    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoundId { get; set; }
    public OwnershipOfferStatus Status { get; set; } = OwnershipOfferStatus.Pending;
    public DateTime OfferedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Circle Circle { get; set; } = null!;
    public User User { get; set; } = null!;
}
