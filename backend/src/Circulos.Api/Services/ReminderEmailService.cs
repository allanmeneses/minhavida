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

        var reminders = await db.Reminders
            .Where(x => x.UserId == userId && !x.IsPaused)
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

            if (reminder.LastEmailedFireAt is { } last && last == reminder.NextFireAt)
            {
                continue;
            }

            await email.SendAsync(
                user.Email,
                $"Lembrete: {reminder.Title}",
                $"O lembrete {reminder.Title} pede atencao agora. Voce escolheu receber por e-mail.",
                cancellationToken);
            reminder.LastEmailedFireAt = reminder.NextFireAt;
            sent.Add(reminder.Title);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return sent;
    }
}
