using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<PersonalDocument> Documents => Set<PersonalDocument>();
    public DbSet<LifeArea> LifeAreas => Set<LifeArea>();
    public DbSet<LifeEntry> LifeEntries => Set<LifeEntry>();
    public DbSet<LifeEntryShare> LifeEntryShares => Set<LifeEntryShare>();
    public DbSet<Circle> Circles => Set<Circle>();
    public DbSet<CircleMember> CircleMembers => Set<CircleMember>();
    public DbSet<CircleInvite> CircleInvites => Set<CircleInvite>();
    public DbSet<CircleJoinRequest> CircleJoinRequests => Set<CircleJoinRequest>();
    public DbSet<CirclePost> CirclePosts => Set<CirclePost>();
    public DbSet<CircleLog> CircleLogs => Set<CircleLog>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Shortcut> Shortcuts => Set<Shortcut>();
    public DbSet<ShortcutShare> ShortcutShares => Set<ShortcutShare>();
    public DbSet<ShortcutChangeNotice> ShortcutChangeNotices => Set<ShortcutChangeNotice>();
    public DbSet<SearchQuery> SearchQueries => Set<SearchQuery>();
    public DbSet<SharedList> SharedLists => Set<SharedList>();
    public DbSet<SharedListItem> SharedListItems => Set<SharedListItem>();
    public DbSet<SharedListGrant> SharedListGrants => Set<SharedListGrant>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes => Set<TwoFactorRecoveryCode>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<LivingProfile> LivingProfiles => Set<LivingProfile>();
    public DbSet<RoutineTemplate> RoutineTemplates => Set<RoutineTemplate>();
    public DbSet<RecapShare> RecapShares => Set<RecapShare>();
    public DbSet<BillingEvent> BillingEvents => Set<BillingEvent>();
    public DbSet<SuccessionVote> SuccessionVotes => Set<SuccessionVote>();
    public DbSet<SuccessionBallot> SuccessionBallots => Set<SuccessionBallot>();
    public DbSet<LivingProfileGrant> LivingProfileGrants => Set<LivingProfileGrant>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<AdminAction> AdminActions => Set<AdminAction>();
    public DbSet<OnboardingExample> OnboardingExamples => Set<OnboardingExample>();
    public DbSet<AccountAppeal> AccountAppeals => Set<AccountAppeal>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<GiftIdeaShare> GiftIdeaShares => Set<GiftIdeaShare>();
    public DbSet<DiaryPage> DiaryPages => Set<DiaryPage>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.VacationUntil).HasColumnType("timestamp with time zone");
            entity.Property(x => x.TwoFactorSecret).HasMaxLength(64);
            entity.Property(x => x.CalendarFeedToken).HasMaxLength(64);
            entity.Property(x => x.SubscriptionProviderId).HasMaxLength(80);
            entity.Property(x => x.PaidUntil).HasColumnType("timestamp with time zone");
            entity.Property(x => x.BlockedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.OnboardingCompletedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.GoogleSubject).HasMaxLength(128);
            entity.HasIndex(x => x.GoogleSubject).IsUnique().HasFilter("\"GoogleSubject\" IS NOT NULL");
            entity.HasIndex(x => x.CalendarFeedToken).IsUnique().HasFilter("\"CalendarFeedToken\" IS NOT NULL");
            entity.HasOne(x => x.Profile)
                .WithOne(x => x.User)
                .HasForeignKey<Profile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Documents)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.LifeAreas)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.LifeEntries)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Reminders)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Shortcuts)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany<SearchQuery>()
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany<RefreshToken>()
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany<TwoFactorChallenge>()
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany<TwoFactorRecoveryCode>()
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.SecondaryEmail).HasMaxLength(256);
            entity.Property(x => x.PhotoStorageKey).HasMaxLength(260);
            entity.Property(x => x.PhotoContentType).HasMaxLength(120);
            entity.Property(x => x.PhotoFileName).HasMaxLength(255);
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<PersonalDocument>(entity =>
        {
            entity.ToTable("personal_documents");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(260).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<LifeArea>(entity =>
        {
            entity.ToTable("life_areas");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasMany(x => x.Entries)
                .WithOne(x => x.LifeArea)
                .HasForeignKey(x => x.LifeAreaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LifeEntry>(entity =>
        {
            entity.ToTable("life_entries");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LifeAreaId);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(20000);
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.PhotoStorageKey).HasMaxLength(260);
            entity.Property(x => x.PhotoContentType).HasMaxLength(120);
            entity.Property(x => x.TagsJson).HasMaxLength(800).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.HasMany(x => x.Shares)
                .WithOne(x => x.LifeEntry)
                .HasForeignKey(x => x.LifeEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LifeEntryShare>(entity =>
        {
            entity.ToTable("life_entry_shares");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.LifeEntryId, x.CircleId }).IsUnique();
            entity.HasIndex(x => x.CircleId);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Circle>(entity =>
        {
            entity.ToTable("circles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OwnerUserId);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasMany<SuccessionVote>()
                .WithOne(x => x.Circle)
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.RevealedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.EndsAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasMany(x => x.Members)
                .WithOne(x => x.Circle)
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Invites)
                .WithOne(x => x.Circle)
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Posts)
                .WithOne(x => x.Circle)
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Logs)
                .WithOne(x => x.Circle)
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany<SharedList>()
                .WithOne(x => x.Circle)
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CircleMember>(entity =>
        {
            entity.ToTable("circle_members");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CircleId, x.UserId }).IsUnique();
            entity.Property(x => x.JoinedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CircleInvite>(entity =>
        {
            entity.ToTable("circle_invites");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.IsOpenLink);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.AcceptedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<CircleJoinRequest>(entity =>
        {
            entity.ToTable("circle_join_requests");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CircleId, x.UserId });
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CirclePost>(entity =>
        {
            entity.ToTable("circle_posts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Body).HasMaxLength(8000);
            entity.Property(x => x.PhotoStorageKey).HasMaxLength(260);
            entity.Property(x => x.PhotoContentType).HasMaxLength(120);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CircleLog>(entity =>
        {
            entity.ToTable("circle_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CircleId);
            entity.Property(x => x.Action).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.ToTable("reminders");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RecurrenceJson).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.PersonName).HasMaxLength(120);
            entity.Property(x => x.GiftIdeasJson).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.PhotoStorageKey).HasMaxLength(260);
            entity.Property(x => x.PhotoContentType).HasMaxLength(120);
            entity.Property(x => x.StartAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.NextFireAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UntilAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.LastEmailedFireAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Shortcut>(entity =>
        {
            entity.ToTable("shortcuts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Label).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Color).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.DeletedAt).HasColumnType("timestamp with time zone");
            entity.HasOne<Reminder>()
                .WithMany()
                .HasForeignKey(x => x.ReminderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Shares)
                .WithOne(x => x.Shortcut)
                .HasForeignKey(x => x.ShortcutId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShortcutShare>(entity =>
        {
            entity.ToTable("shortcut_shares");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ShortcutId, x.CircleId }).IsUnique();
            entity.HasIndex(x => x.CircleId);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShortcutChangeNotice>(entity =>
        {
            entity.ToTable("shortcut_change_notices");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RecipientUserId);
            entity.HasIndex(x => x.ShortcutId);
            entity.Property(x => x.Detail).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Shortcut)
                .WithMany()
                .HasForeignKey(x => x.ShortcutId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SearchQuery>(entity =>
        {
            entity.ToTable("search_queries");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.Term }).IsUnique();
            entity.Property(x => x.Term).HasMaxLength(120).IsRequired();
            entity.Property(x => x.LastUsedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<SharedList>(entity =>
        {
            entity.ToTable("shared_lists");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OwnerUserId);
            entity.HasIndex(x => x.CircleId);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasMany(x => x.Items)
                .WithOne(x => x.List)
                .HasForeignKey(x => x.ListId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Grants)
                .WithOne(x => x.List)
                .HasForeignKey(x => x.ListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SharedListGrant>(entity =>
        {
            entity.ToTable("shared_list_grants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ListId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<SharedListItem>(entity =>
        {
            entity.ToTable("shared_list_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ListId);
            entity.Property(x => x.Text).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Quantity).HasMaxLength(80);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.DoneAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.ToTable("password_resets");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UsedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountDeletionRequest>(entity =>
        {
            entity.ToTable("account_deletion_requests");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UsedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.FamilyId);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.RevokedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<TwoFactorChallenge>(entity =>
        {
            entity.ToTable("two_factor_challenges");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UsedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<TwoFactorRecoveryCode>(entity =>
        {
            entity.ToTable("two_factor_recovery_codes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.CodeHash }).IsUnique();
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UsedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Memory>(entity =>
        {
            entity.ToTable("memories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AuthorUserId);
            entity.HasIndex(x => x.CircleId);
            entity.HasIndex(x => new { x.AuthorUserId, x.SourceCircleId })
                .IsUnique()
                .HasFilter("\"SourceCircleId\" IS NOT NULL");
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.PhotoStorageKey).HasMaxLength(260);
            entity.Property(x => x.PhotoContentType).HasMaxLength(120);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LivingProfile>(entity =>
        {
            entity.ToTable("living_profiles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OwnerUserId);
            entity.HasIndex(x => x.CircleId);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Context).HasMaxLength(200);
            entity.Property(x => x.PhotoStorageKey).HasMaxLength(260);
            entity.Property(x => x.PhotoContentType).HasMaxLength(120);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Grants)
                .WithOne(x => x.LivingProfile)
                .HasForeignKey(x => x.LivingProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LivingProfileGrant>(entity =>
        {
            entity.ToTable("living_profile_grants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.LivingProfileId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<SuccessionVote>(entity =>
        {
            entity.ToTable("succession_votes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CircleId);
            entity.Property(x => x.OpenedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.DeadlineAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.ClosedAt).HasColumnType("timestamp with time zone");
            entity.HasMany(x => x.Ballots)
                .WithOne(x => x.Vote)
                .HasForeignKey(x => x.VoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SuccessionBallot>(entity =>
        {
            entity.ToTable("succession_ballots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.VoteId, x.VoterUserId }).IsUnique();
            entity.Property(x => x.CastAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<BillingEvent>(entity =>
        {
            entity.ToTable("billing_events");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Provider, x.EventKey }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EventKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Topic).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ResourceId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Result).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReceivedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<RoutineTemplate>(entity =>
        {
            entity.ToTable("routine_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PayloadJson).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<RecapShare>(entity =>
        {
            entity.ToTable("recap_shares");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Token).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SentEmail>(entity =>
        {
            entity.ToTable("sent_emails");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.To).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.SentAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<UsageCounter>(entity =>
        {
            entity.ToTable("usage_counters");
            entity.HasKey(x => x.Metric);
            entity.Property(x => x.Metric).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<ContentReport>(entity =>
        {
            entity.ToTable("content_reports");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKind, x.TargetId });
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.TargetKind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CircleDecision).HasMaxLength(20);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CircleReviewedAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.CircleId);
            entity.HasOne(x => x.Reporter)
                .WithMany()
                .HasForeignKey(x => x.ReporterUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminAction>(entity =>
        {
            entity.ToTable("admin_actions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Action).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TargetKind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Admin)
                .WithMany()
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OnboardingExample>(entity =>
        {
            entity.ToTable("onboarding_examples");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.ConfirmedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.DiscardedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountAppeal>(entity =>
        {
            entity.ToTable("account_appeals");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Note).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.ResolvedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BackgroundJob>(entity =>
        {
            entity.ToTable("background_jobs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.RunAt });
            entity.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Payload).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RunAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<GiftIdeaShare>(entity =>
        {
            entity.ToTable("gift_idea_shares");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ReminderId, x.CircleId }).IsUnique();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Reminder)
                .WithMany()
                .HasForeignKey(x => x.ReminderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Circle)
                .WithMany()
                .HasForeignKey(x => x.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DiaryPage>(entity =>
        {
            entity.ToTable("diary_pages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LifeAreaId);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(20000).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.ToTable("support_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });
    }
}
