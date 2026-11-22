using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<PersonalDocument> Documents => Set<PersonalDocument>();

    // Proximas versoes abrem o modelo a partir de User:
    // Circulo (organizador = User), MembroDeCirculo e Lembrete (dono = User).
    // Essas tabelas nao existem na v1.

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
            entity.HasOne(x => x.Profile)
                .WithOne(x => x.User)
                .HasForeignKey<Profile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Documents)
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
    }
}
