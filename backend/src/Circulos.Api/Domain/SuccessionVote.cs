namespace Circulos.Api.Domain;

public enum SuccessionVoteStatus
{
    Open = 0,
    Decided = 1,
    Expired = 2,
}

public sealed class SuccessionVote
{
    public const int DeadlineDays = 3;
    public const int ReplacementCooldownDays = 30;

    public Guid Id { get; set; }
    public Guid CircleId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public SuccessionVoteStatus Status { get; set; } = SuccessionVoteStatus.Open;
    public Guid? WinnerUserId { get; set; }
    public DateTime? ClosedAt { get; set; }

    public Circle Circle { get; set; } = null!;
    public ICollection<SuccessionBallot> Ballots { get; set; } = new List<SuccessionBallot>();
}

public sealed class SuccessionBallot
{
    public Guid Id { get; set; }
    public Guid VoteId { get; set; }
    public Guid VoterUserId { get; set; }
    public Guid CandidateUserId { get; set; }
    public DateTime CastAt { get; set; }

    public SuccessionVote Vote { get; set; } = null!;
}
