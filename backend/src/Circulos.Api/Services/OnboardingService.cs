using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class OnboardingService(AppDbContext db, TimeProvider clock)
{
    public void Seed(User user, DateTime now)
    {
        var eu = CreateCircle(user, "Eu", "Seu espaco pessoal, só você.", now);
        var familia = CreateCircle(user, "Família", "Combine a rotina de quem mora junto.", now);
        db.Circles.Add(eu);
        db.Circles.Add(familia);
        db.OnboardingExamples.Add(Example(user.Id, eu.Id, "diary", "Como foi o meu dia", "Escreva em uma frase o que marcou hoje.", now));
        db.OnboardingExamples.Add(Example(user.Id, eu.Id, "memory", "Uma data que importa", "Guarde uma memória antiga, mesmo que curta.", now));
        db.OnboardingExamples.Add(Example(user.Id, familia.Id, "post", "O que a família combinou", "Um recado simples para quem está neste círculo.", now));
    }

    public async Task<OnboardingSnapshot> SnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var examples = await db.OnboardingExamples.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var circleIds = examples.Where(item => item.CircleId is not null).Select(item => item.CircleId!.Value).Distinct().ToList();
        var names = circleIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Circles.AsNoTracking()
                .Where(circle => circleIds.Contains(circle.Id))
                .ToDictionaryAsync(circle => circle.Id, circle => circle.Name, cancellationToken);
        return new OnboardingSnapshot(examples, names);
    }

    public async Task<string?> DecideAsync(Guid userId, Guid exampleId, bool confirm, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var example = await db.OnboardingExamples.FirstOrDefaultAsync(
            item => item.Id == exampleId && item.UserId == userId,
            cancellationToken);
        if (example is null)
        {
            return "Exemplo não encontrado.";
        }

        if (example.ConfirmedAt is not null || example.DiscardedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (!confirm)
        {
            example.DiscardedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await CompleteIfDone(userId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        example.ConfirmedAt = now;
        if (example.Kind == "diary")
        {
            var diary = await db.LifeAreas.FirstAsync(
                area => area.UserId == userId && area.Kind == LifeAreaKind.Diary,
                cancellationToken);
            db.LifeEntries.Add(new LifeEntry
            {
                Id = Guid.NewGuid(),
                LifeAreaId = diary.Id,
                UserId = userId,
                Title = example.Title,
                Body = example.Body,
                OccurredOn = DateOnly.FromDateTime(now),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else if (example.Kind == "memory")
        {
            db.Memories.Add(new Memory
            {
                Id = Guid.NewGuid(),
                AuthorUserId = userId,
                CircleId = example.CircleId,
                Title = example.Title,
                Body = example.Body,
                OccurredOn = DateOnly.FromDateTime(now),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else if (example.Kind == "post" && example.CircleId is Guid circleId)
        {
            db.CirclePosts.Add(new CirclePost
            {
                Id = Guid.NewGuid(),
                CircleId = circleId,
                AuthorUserId = userId,
                Body = example.Body,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await CompleteIfDone(userId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private async Task CompleteIfDone(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var pending = await db.OnboardingExamples.CountAsync(
            item => item.UserId == userId && item.ConfirmedAt == null && item.DiscardedAt == null,
            cancellationToken);
        if (pending == 0)
        {
            var user = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
            user.OnboardingCompletedAt ??= now;
        }
    }

    private static Circle CreateCircle(User user, string name, string description, DateTime now)
    {
        var circle = new Circle
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Name = name,
            Description = description,
            Visibility = CircleVisibility.Private,
            Discoverable = false,
            Kind = CircleKind.Lasting,
            Status = CircleStatus.Active,
            IsStarter = true,
            CreatedAt = now,
        };
        circle.Members.Add(new CircleMember
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            UserId = user.Id,
            Role = CircleRole.Owner,
            JoinedAt = now,
        });
        circle.Logs.Add(new CircleLog
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            ActorUserId = user.Id,
            Action = "created",
            Detail = "Círculo inicial criado no cadastro.",
            CreatedAt = now,
        });
        return circle;
    }

    private static OnboardingExample Example(Guid userId, Guid circleId, string kind, string title, string body, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CircleId = circleId,
            Kind = kind,
            Title = title,
            Body = body,
            CreatedAt = now,
        };
}

public sealed record OnboardingSnapshot(
    IReadOnlyList<OnboardingExample> Examples,
    IReadOnlyDictionary<Guid, string> CircleNames);
