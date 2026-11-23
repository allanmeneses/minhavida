using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class ReminderEmailService(AppDbContext db, TimeProvider clock, IEmailSender email)
{
    public async Task<IReadOnlyList<string>> DispatchDueAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (!user.EmailRemindersEnabled || (user.VacationUntil is { } until && until > now))
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        var memberCircleIds = await db.CircleMembers
            .Where(x => x.UserId == userId && x.Circle.Status == CircleStatus.Active)
            .Select(x => x.CircleId)
            .ToListAsync(cancellationToken);
        var reminders = await db.Reminders
            .Where(x => !x.IsPaused
                && (x.UserId == userId
                    || (x.CircleId != null && memberCircleIds.Contains(x.CircleId.Value))))
            .ToListAsync(cancellationToken);
        var sent = new List<string>();
        foreach (var reminder in reminders)
        {
            var fireAt = reminder.Kind == ReminderKind.Birthday
                ? reminder.NextFireAt.AddDays(-reminder.AdvanceDays)
                : reminder.NextFireAt;
            if (fireAt > now)
            {
                continue;
            }

            var already = await db.ReminderEmailReceipts.AnyAsync(
                x => x.ReminderId == reminder.Id && x.UserId == userId && x.FireAt == reminder.NextFireAt,
                cancellationToken);
            if (already || (reminder.LastEmailedFireAt is { } last && last == reminder.NextFireAt && reminder.UserId == userId))
            {
                continue;
            }

            await email.SendAsync(
                user.Email,
                $"Lembrete: {reminder.Title}",
                $"O lembrete {reminder.Title} pede atenção agora. Você escolheu receber por e-mail. Sem SMS.",
                cancellationToken);
            db.ReminderEmailReceipts.Add(new ReminderEmailReceipt
            {
                Id = Guid.NewGuid(),
                ReminderId = reminder.Id,
                UserId = userId,
                FireAt = reminder.NextFireAt,
                SentAt = now,
            });
            if (reminder.UserId == userId)
            {
                reminder.LastEmailedFireAt = reminder.NextFireAt;
            }

            sent.Add(reminder.Title);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return sent;
    }
}
