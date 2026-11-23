using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class CircleArchiveService(AppDbContext db, TimeProvider clock)
{
    public async Task ArchiveExpiredAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var dueIds = await db.Circles
            .AsNoTracking()
            .Where(circle =>
                circle.Status == CircleStatus.Active
                && circle.EndsAt != null
                && circle.EndsAt <= now
                && (circle.Kind == CircleKind.Temporary || circle.Kind == CircleKind.Daily))
            .Select(circle => circle.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in dueIds)
        {
            await ArchiveIfDueAsync(id, cancellationToken);
        }
    }

    public async Task<bool> ArchiveIfDueAsync(Guid circleId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await CircleRowLock.AcquireAsync(db, circleId, cancellationToken);
        var circle = await db.Circles.FirstOrDefaultAsync(item => item.Id == circleId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (circle is null
            || circle.Status != CircleStatus.Active
            || circle.EndsAt is null
            || circle.EndsAt > now
            || (circle.Kind != CircleKind.Temporary && circle.Kind != CircleKind.Daily))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        circle.Status = CircleStatus.Archived;
        db.CircleLogs.Add(new CircleLog
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            ActorUserId = null,
            Action = "archived",
            Detail = "Ciclo encerrado. A retrospectiva fica para consulta.",
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
