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
    public DbSet<Circle> Circles => Set<Circle>();
    public DbSet<CircleMember> CircleMembers => Set<CircleMember>();
    public DbSet<CircleInvite> CircleInvites => Set<CircleInvite>();
    public DbSet<CircleJoinRequest> CircleJoinRequests => Set<CircleJoinRequest>();
    public DbSet<CirclePost> CirclePosts => Set<CirclePost>();
    public DbSet<CircleLog> CircleLogs => Set<CircleLog>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Shortcut> Shortcuts => Set<Shortcut>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();

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
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
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
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Circle>(entity =>
        {
            entity.ToTable("circles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OwnerUserId);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.RevealedAt).HasColumnType("timestamp with time zone");
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
            entity.Property(x => x.StartAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.NextFireAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UntilAt).HasColumnType("timestamp with time zone");
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

        modelBuilder.Entity<SentEmail>(entity =>
        {
            entity.ToTable("sent_emails");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.To).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.SentAt).HasColumnType("timestamp with time zone");
        });
    }
}
