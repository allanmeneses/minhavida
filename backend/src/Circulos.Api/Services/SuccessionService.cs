using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class SuccessionService(AppDbContext db, TimeProvider clock)
{
    public async Task<SuccessionVote?> OpenIfNeededAsync(Circle circle, CancellationToken cancellationToken)
    {
        var remaining = circle.Members.Where(member => member.UserId != circle.OwnerUserId).ToList();
        if (remaining.Count == 0)
        {
            return null;
        }

        var open = await db.Set<SuccessionVote>().FirstOrDefaultAsync(
            vote => vote.CircleId == circle.Id && vote.Status == SuccessionVoteStatus.Open,
            cancellationToken);
        if (open is not null)
        {
            return open;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var vote = new SuccessionVote
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            OpenedAt = now,
            DeadlineAt = now.AddDays(SuccessionVote.DeadlineDays),
            Status = SuccessionVoteStatus.Open,
        };
        db.Set<SuccessionVote>().Add(vote);
        db.CircleLogs.Add(new CircleLog
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            ActorUserId = null,
            Action = "succession-opened",
            Detail = "Sem sucessor. Aberta votacao por maioria simples, sem quorum, prazo de 3 dias. Todos os membros votam.",
            CreatedAt = now,
        });
        return vote;
    }

    public async Task<SuccessionVote?> CurrentAsync(Guid circleId, CancellationToken cancellationToken)
    {
        var vote = await db.Set<SuccessionVote>()
            .Include(item => item.Ballots)
            .Where(item => item.CircleId == circleId)
            .OrderByDescending(item => item.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (vote is null)
        {
            return null;
        }

        await CloseIfDueAsync(vote, cancellationToken);
        return vote;
    }

    public async Task<(int Status, string? Error)> CastAsync(Guid circleId, Guid voterId, Guid candidateId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, circleId, cancellationToken);
        var circle = await db.Circles.Include(item => item.Members).FirstOrDefaultAsync(item => item.Id == circleId, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted)
        {
            return (StatusCodes.Status404NotFound, "Circulo nao encontrado.");
        }

        var members = circle.Members.ToList();
        if (members.All(member => member.UserId != voterId) || members.All(member => member.UserId != candidateId))
        {
            return (StatusCodes.Status400BadRequest, "So membros do circulo votam e so em membros.");
        }

        var vote = await db.Set<SuccessionVote>()
            .Include(item => item.Ballots)
            .Where(item => item.CircleId == circleId)
            .OrderByDescending(item => item.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (vote is null)
        {
            return (StatusCodes.Status404NotFound, "Nao ha votacao aberta.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (vote.Status == SuccessionVoteStatus.Decided)
        {
            await transaction.CommitAsync(cancellationToken);
            return vote.WinnerUserId == candidateId
                ? (StatusCodes.Status204NoContent, null)
                : (StatusCodes.Status400BadRequest, "A votacao ja elegeu o novo dono.");
        }

        if (vote.Status == SuccessionVoteStatus.Expired || now >= vote.DeadlineAt)
        {
            if (vote.Status == SuccessionVoteStatus.Open)
            {
                await ExpireAsync(circle, vote, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return (StatusCodes.Status400BadRequest, "O prazo de 3 dias acabou.");
        }

        var existing = await db.SuccessionBallots.FirstOrDefaultAsync(
            ballot => ballot.VoteId == vote.Id && ballot.VoterUserId == voterId,
            cancellationToken);
        if (existing is not null)
        {
            existing.CandidateUserId = candidateId;
            existing.CastAt = now;
        }
        else
        {
            existing = new SuccessionBallot
            {
                Id = Guid.NewGuid(),
                VoteId = vote.Id,
                VoterUserId = voterId,
                CandidateUserId = candidateId,
                CastAt = now,
            };
            db.SuccessionBallots.Add(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
        if (vote.Ballots.All(ballot => ballot.VoterUserId != voterId))
        {
            vote.Ballots.Add(existing);
        }
        await TryDecideAsync(circle, vote, members.Count, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (StatusCodes.Status204NoContent, null);
    }

    public async Task<(int Status, string? Error)> RequestReplacementAsync(
        Guid circleId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, circleId, cancellationToken);
        var circle = await db.Circles.Include(item => item.Members).FirstOrDefaultAsync(item => item.Id == circleId, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted)
        {
            return (StatusCodes.Status404NotFound, "Circulo nao encontrado.");
        }

        if (circle.Members.All(member => member.UserId != requesterId))
        {
            return (StatusCodes.Status404NotFound, "Circulo nao encontrado.");
        }

        if (circle.OwnerElectedAt is null || circle.OwnerUserId is null)
        {
            return (StatusCodes.Status400BadRequest, "So o dono eleito por votacao pode ser substituido assim.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var availableAt = circle.OwnerElectedAt.Value.AddDays(SuccessionVote.ReplacementCooldownDays);
        if (now < availableAt)
        {
            return (StatusCodes.Status400BadRequest, "O dono eleito so pode ser substituido apos 30 dias da eleicao.");
        }

        var open = await db.Set<SuccessionVote>().FirstOrDefaultAsync(
            vote => vote.CircleId == circleId && vote.Status == SuccessionVoteStatus.Open,
            cancellationToken);
        if (open is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return (StatusCodes.Status204NoContent, null);
        }

        var vote = new SuccessionVote
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            OpenedAt = now,
            DeadlineAt = now.AddDays(SuccessionVote.DeadlineDays),
            Status = SuccessionVoteStatus.Open,
        };
        db.Set<SuccessionVote>().Add(vote);
        db.CircleLogs.Add(new CircleLog
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            ActorUserId = requesterId,
            Action = "succession-opened",
            Detail = "Membro pediu nova votacao para substituir o dono eleito. Maioria simples, sem quorum.",
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (StatusCodes.Status204NoContent, null);
    }

    public async Task CloseIfDueAsync(SuccessionVote vote, CancellationToken cancellationToken)
    {
        if (vote.Status != SuccessionVoteStatus.Open)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (now < vote.DeadlineAt)
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, vote.CircleId, cancellationToken);
        await db.Entry(vote).ReloadAsync(cancellationToken);
        if (vote.Status != SuccessionVoteStatus.Open)
        {
            return;
        }

        now = clock.GetUtcNow().UtcDateTime;
        if (now < vote.DeadlineAt)
        {
            return;
        }

        var circle = await db.Circles.Include(item => item.Members).FirstAsync(item => item.Id == vote.CircleId, cancellationToken);
        await ExpireAsync(circle, vote, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CloseExpiredLockedAsync(Circle circle, CancellationToken cancellationToken)
    {
        var vote = await db.Set<SuccessionVote>()
            .Include(item => item.Ballots)
            .Where(item => item.CircleId == circle.Id && item.Status == SuccessionVoteStatus.Open)
            .OrderByDescending(item => item.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (vote is null)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (now < vote.DeadlineAt)
        {
            return;
        }

        await ExpireAsync(circle, vote, now, cancellationToken);
    }

    private async Task TryDecideAsync(Circle circle, SuccessionVote vote, int memberCount, DateTime now, CancellationToken cancellationToken)
    {
        if (memberCount <= 0)
        {
            return;
        }

        var uniqueVoters = vote.Ballots.Select(ballot => ballot.VoterUserId).Distinct().Count();
        if (uniqueVoters < memberCount)
        {
            return;
        }

        var leader = UniqueLeader(vote);
        if (leader is null)
        {
            return;
        }

        await ElectAsync(circle, vote, leader.Value, now, cancellationToken);
    }

    private async Task ElectAsync(Circle circle, SuccessionVote vote, Guid winnerId, DateTime now, CancellationToken cancellationToken)
    {
        await CircleOwnership.AssignAsync(
            db,
            circle,
            winnerId,
            now,
            "succession-decided",
            "Maioria simples, sem quorum, elegeu o novo dono. Historico de votacoes anteriores nao fica visivel.",
            cancellationToken);
        vote.Status = SuccessionVoteStatus.Decided;
        vote.WinnerUserId = winnerId;
        vote.ClosedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireAsync(Circle circle, SuccessionVote vote, DateTime now, CancellationToken cancellationToken)
    {
        if (vote.Status != SuccessionVoteStatus.Open)
        {
            return;
        }

        var leader = UniqueLeader(vote);
        if (leader is not null)
        {
            await ElectAsync(circle, vote, leader.Value, now, cancellationToken);
            return;
        }

        vote.Status = SuccessionVoteStatus.Expired;
        vote.ClosedAt = now;
        if (circle.OwnerUserId is null)
        {
            if (circle.OwnerLostAt is null)
            {
                CircleOwnership.MarkLost(circle, now);
            }

            db.CircleLogs.Add(new CircleLog
            {
                Id = Guid.NewGuid(),
                CircleId = circle.Id,
                ActorUserId = null,
                Action = "succession-expired",
                Detail = "Prazo da votacao acabou sem eleger. O circulo segue sem dono; a posse e oferecida as pessoas mais ativas apos 40 dias.",
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid? UniqueLeader(SuccessionVote vote)
    {
        var grouped = vote.Ballots.GroupBy(ballot => ballot.CandidateUserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToList();
        var leader = grouped.FirstOrDefault();
        if (leader is null || leader.Count <= 0)
        {
            return null;
        }

        if (grouped.Count(item => item.Count == leader.Count) != 1)
        {
            return null;
        }

        return leader.UserId;
    }
}
