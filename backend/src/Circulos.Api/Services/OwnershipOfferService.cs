using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class OwnershipOfferService(AppDbContext db, TimeProvider clock, IEmailSender email, SuccessionService succession)
{
    public const int WaitDays = 40;
    public const int BatchSize = 3;

    public async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.Circles.AsNoTracking()
            .Where(circle =>
                circle.OwnerUserId == null
                && circle.Status != CircleStatus.Deleted
                && circle.Status != CircleStatus.Archived
                && circle.Status != CircleStatus.Blocked
                && circle.OwnershipNextCheckAt != null
                && circle.OwnershipNextCheckAt <= now)
            .Select(circle => circle.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await ProcessCircleAsync(id, cancellationToken);
        }
    }

    public async Task<(int Status, string? Error)> AcceptAsync(
        Guid circleId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, circleId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var circle = await db.Circles
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == circleId, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted)
        {
            return (StatusCodes.Status404NotFound, "Círculo não encontrado.");
        }

        if (circle.Members.All(member => member.UserId != userId))
        {
            return (StatusCodes.Status404NotFound, "Círculo não encontrado.");
        }

        var offer = await db.OwnershipOffers.FirstOrDefaultAsync(
            item => item.CircleId == circleId && item.UserId == userId && item.Status == OwnershipOfferStatus.Pending,
            cancellationToken);
        if (offer is null || offer.ExpiresAt <= now)
        {
            return (StatusCodes.Status400BadRequest, "Esta oferta de posse não está mais aberta.");
        }

        if (circle.OwnerUserId is not null)
        {
            offer.Status = OwnershipOfferStatus.Cancelled;
            offer.RespondedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (StatusCodes.Status400BadRequest, "O círculo já tem dono.");
        }

        var openVote = await db.SuccessionVotes.AnyAsync(
            vote => vote.CircleId == circleId && vote.Status == SuccessionVoteStatus.Open,
            cancellationToken);
        if (openVote)
        {
            return (StatusCodes.Status400BadRequest, "A votação de sucessão ainda está aberta.");
        }

        await CircleOwnership.AssignAsync(
            db,
            circle,
            userId,
            now,
            "ownership-accepted",
            "Aceitou a oferta de posse. Os outros convites desta rodada foram cancelados na hora.",
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (StatusCodes.Status204NoContent, null);
    }

    public async Task<OwnershipOffer?> CurrentForUserAsync(
        Guid circleId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.OwnershipOffers.FirstOrDefaultAsync(
            offer => offer.CircleId == circleId
                && offer.UserId == userId
                && offer.Status == OwnershipOfferStatus.Pending
                && offer.ExpiresAt > now,
            cancellationToken);
    }

    public async Task<List<OwnershipOffer>> OpenForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.OwnershipOffers
            .Include(offer => offer.Circle)
            .Where(offer =>
                offer.UserId == userId
                && offer.Status == OwnershipOfferStatus.Pending
                && offer.ExpiresAt > now
                && offer.Circle.OwnerUserId == null
                && offer.Circle.Status != CircleStatus.Deleted)
            .OrderBy(offer => offer.OfferedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessCircleAsync(Guid circleId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, circleId, cancellationToken);
        var circle = await db.Circles
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == circleId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (circle is null
            || circle.OwnerUserId is not null
            || circle.Status is CircleStatus.Deleted or CircleStatus.Archived or CircleStatus.Blocked
            || circle.OwnershipNextCheckAt is null
            || circle.OwnershipNextCheckAt > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await succession.CloseExpiredLockedAsync(circle, cancellationToken);
        circle = await db.Circles.Include(item => item.Members).FirstAsync(item => item.Id == circleId, cancellationToken);
        if (circle.OwnerUserId is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var openVote = await db.SuccessionVotes
            .Include(vote => vote.Ballots)
            .Where(vote => vote.CircleId == circleId && vote.Status == SuccessionVoteStatus.Open)
            .OrderByDescending(vote => vote.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (openVote is not null)
        {
            var lostLongEnough = circle.OwnerLostAt is DateTime lost && lost.AddDays(WaitDays) <= now;
            if (!lostLongEnough)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            openVote.DeadlineAt = now;
            await succession.CloseExpiredLockedAsync(circle, cancellationToken);
            circle = await db.Circles.Include(item => item.Members).FirstAsync(item => item.Id == circleId, cancellationToken);
            if (circle.OwnerUserId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var stillOpen = await db.SuccessionVotes.AnyAsync(
                vote => vote.CircleId == circleId && vote.Status == SuccessionVoteStatus.Open,
                cancellationToken);
            if (stillOpen)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
        }

        var pending = await db.OwnershipOffers
            .Where(offer => offer.CircleId == circleId && offer.Status == OwnershipOfferStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var offer in pending.Where(item => item.ExpiresAt <= now))
        {
            offer.Status = OwnershipOfferStatus.Expired;
            offer.RespondedAt = now;
        }

        if (pending.Any(item => item.Status == OwnershipOfferStatus.Pending && item.ExpiresAt > now))
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var lastRoundId = await db.OwnershipOffers
            .Where(offer => offer.CircleId == circleId)
            .OrderByDescending(offer => offer.OfferedAt)
            .Select(offer => (Guid?)offer.RoundId)
            .FirstOrDefaultAsync(cancellationToken);
        var skip = lastRoundId is Guid round
            ? await db.OwnershipOffers
                .Where(offer => offer.CircleId == circleId && offer.RoundId == round)
                .Select(offer => offer.UserId)
                .ToListAsync(cancellationToken)
            : [];

        var ranked = await RankMembersAsync(circle, cancellationToken);
        var next = ranked.Where(item => !skip.Contains(item.UserId)).Take(BatchSize).ToList();
        if (next.Count == 0)
        {
            next = ranked.Take(BatchSize).ToList();
        }

        if (next.Count == 0)
        {
            circle.OwnershipNextCheckAt = now.AddDays(WaitDays);
            db.CircleLogs.Add(new CircleLog
            {
                Id = Guid.NewGuid(),
                CircleId = circle.Id,
                ActorUserId = null,
                Action = "ownership-offer-skipped",
                Detail = "Sem membros para oferecer a posse. Nova checagem em 40 dias.",
                CreatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var roundId = Guid.NewGuid();
        var created = new List<(OwnershipOffer Offer, string Email, string CircleName)>();
        foreach (var member in next)
        {
            var user = await db.Users.FirstAsync(item => item.Id == member.UserId, cancellationToken);
            var offer = new OwnershipOffer
            {
                Id = Guid.NewGuid(),
                CircleId = circle.Id,
                UserId = member.UserId,
                RoundId = roundId,
                Status = OwnershipOfferStatus.Pending,
                OfferedAt = now,
                ExpiresAt = now.AddDays(WaitDays),
            };
            db.OwnershipOffers.Add(offer);
            created.Add((offer, user.Email, circle.Name));
        }

        circle.OwnershipNextCheckAt = now.AddDays(WaitDays);
        db.CircleLogs.Add(new CircleLog
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            ActorUserId = null,
            Action = "ownership-offered",
            Detail = next.Count == 1
                ? "Oferta de posse enviada para o membro restante."
                : $"Oferta de posse enviada para as {next.Count} pessoas mais ativas do círculo.",
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var item in created)
        {
            await email.SendAsync(
                item.Email,
                $"Convite para assumir o círculo {item.CircleName}",
                $"O círculo {item.CircleName} ficou sem dono. Você está entre as pessoas mais ativas e pode aceitar a posse no app. O primeiro que aceitar vira dono; os outros convites saem na hora.",
                cancellationToken);
        }
    }

    public async Task<List<(Guid UserId, int Score, DateTime LastAt, DateTime JoinedAt)>> RankMembersAsync(
        Circle circle,
        CancellationToken cancellationToken)
    {
        var memberIds = circle.Members.Select(member => member.UserId).ToList();
        if (memberIds.Count == 0)
        {
            return [];
        }

        var posts = await db.CirclePosts.AsNoTracking()
            .Where(post => post.CircleId == circle.Id && post.AuthorUserId != null && memberIds.Contains(post.AuthorUserId.Value))
            .GroupBy(post => post.AuthorUserId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count(), Last = group.Max(item => item.CreatedAt) })
            .ToListAsync(cancellationToken);
        var logs = await db.CircleLogs.AsNoTracking()
            .Where(log => log.CircleId == circle.Id && log.ActorUserId != null && memberIds.Contains(log.ActorUserId.Value))
            .GroupBy(log => log.ActorUserId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count(), Last = group.Max(item => item.CreatedAt) })
            .ToListAsync(cancellationToken);
        var memories = await db.Memories.AsNoTracking()
            .Where(memory => memory.CircleId == circle.Id && memberIds.Contains(memory.AuthorUserId))
            .GroupBy(memory => memory.AuthorUserId)
            .Select(group => new { UserId = group.Key, Count = group.Count(), Last = group.Max(item => item.CreatedAt) })
            .ToListAsync(cancellationToken);

        var postMap = posts.ToDictionary(item => item.UserId);
        var logMap = logs.ToDictionary(item => item.UserId);
        var memoryMap = memories.ToDictionary(item => item.UserId);

        return circle.Members
            .Select(member =>
            {
                postMap.TryGetValue(member.UserId, out var post);
                logMap.TryGetValue(member.UserId, out var log);
                memoryMap.TryGetValue(member.UserId, out var memory);
                var score = (post?.Count ?? 0) + (log?.Count ?? 0) + (memory?.Count ?? 0);
                var last = new[] { post?.Last, log?.Last, memory?.Last, member.JoinedAt }
                    .Where(value => value is not null)
                    .Select(value => value!.Value)
                    .DefaultIfEmpty(member.JoinedAt)
                    .Max();
                return (member.UserId, score, last, member.JoinedAt);
            })
            .OrderByDescending(item => item.score)
            .ThenByDescending(item => item.last)
            .ThenBy(item => item.JoinedAt)
            .ThenBy(item => item.UserId)
            .ToList();
    }
}
