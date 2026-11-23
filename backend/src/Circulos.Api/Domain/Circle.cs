namespace Circulos.Api.Domain;

public sealed class Circle
{
    public Guid Id { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? SuccessorUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CircleVisibility Visibility { get; set; } = CircleVisibility.Private;
    public bool Discoverable { get; set; }
    public bool FindableByLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsSurprise { get; set; }
    public bool IsStarter { get; set; }
    public Guid? SurpriseSubjectUserId { get; set; }
    public DateTime? RevealedAt { get; set; }
    public CircleKind Kind { get; set; } = CircleKind.Lasting;
    public CircleStatus Status { get; set; } = CircleStatus.Active;
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? Owner { get; set; }
    public ICollection<CircleMember> Members { get; set; } = new List<CircleMember>();
    public ICollection<CircleInvite> Invites { get; set; } = new List<CircleInvite>();
    public ICollection<CirclePost> Posts { get; set; } = new List<CirclePost>();
    public ICollection<CircleLog> Logs { get; set; } = new List<CircleLog>();
}
