namespace Circulos.Api.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public PlanKind Plan { get; set; } = PlanKind.Free;
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public BillingPeriod? BillingPeriod { get; set; }
    public string? SubscriptionProviderId { get; set; }
    public DateTime? PaidUntil { get; set; }
    public long StorageUsedBytes { get; set; }
    public int OwnedCircleCount { get; set; }
    public DateTime? VacationUntil { get; set; }
    public bool EmailRemindersEnabled { get; set; }
    public bool IsSystemAdmin { get; set; }
    public DateTime? BlockedAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? CalendarFeedToken { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
    public DateOnly? LastMonthlyRecapSeenOn { get; set; }
    public DateOnly? LastRoutineDate { get; set; }
    public int RoutineStreakDays { get; set; }
    public string? GoogleSubject { get; set; }

    public Profile Profile { get; set; } = null!;
    public ICollection<PersonalDocument> Documents { get; set; } = new List<PersonalDocument>();
    public ICollection<LifeArea> LifeAreas { get; set; } = new List<LifeArea>();
    public ICollection<LifeEntry> LifeEntries { get; set; } = new List<LifeEntry>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public ICollection<Shortcut> Shortcuts { get; set; } = new List<Shortcut>();
    public ICollection<RecordType> RecordTypes { get; set; } = new List<RecordType>();
    public ICollection<LifeRecord> Records { get; set; } = new List<LifeRecord>();
}
