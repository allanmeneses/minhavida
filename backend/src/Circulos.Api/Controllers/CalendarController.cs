using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Reminders;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Route("api/calendar")]
public sealed class CalendarController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<CalendarMonthResponse>> Month(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var range = Range(from, to, clock);
        var events = await EventsAsync(userId, range.FromUtc, range.ToUtc, cancellationToken);
        var token = await EnsureFeedTokenAsync(userId, cancellationToken);
        return Ok(new CalendarMonthResponse(
            DateOnly.FromDateTime(range.FromUtc),
            DateOnly.FromDateTime(range.ToUtc.AddSeconds(-1)),
            events,
            $"/api/calendar/feed/{token}"));
    }

    [HttpGet("ics")]
    [Authorize]
    public async Task<IActionResult> Download(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var range = Range(from, to, clock);
        var events = await EventsAsync(userId, range.FromUtc, range.ToUtc, cancellationToken);
        var ics = ToIcs(events, clock.GetUtcNow().UtcDateTime);
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "circulos-lembretes.ics");
    }

    [HttpGet("feed/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Feed(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 16)
        {
            return NotFound();
        }

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CalendarFeedToken == token, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var from = now.AddMonths(-1);
        var to = now.AddMonths(12);
        var events = await EventsAsync(user.Id, from, to, cancellationToken);
        var ics = ToIcs(events, now);
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "circulos-lembretes.ics");
    }

    private async Task<List<CalendarEventResponse>> EventsAsync(
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var reminders = await db.Reminders.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var items = new List<CalendarEventResponse>();
        foreach (var reminder in reminders)
        {
            var rule = RecurrenceRule.Parse(reminder.RecurrenceJson);
            var occurrences = RecurrenceCalculator.OccurrencesInRange(rule, reminder.StartAt, fromUtc, toUtc);
            foreach (var at in occurrences)
            {
                if (at < reminder.NextFireAt)
                {
                    continue;
                }

                items.Add(new CalendarEventResponse(
                    reminder.Id,
                    reminder.Title,
                    EnumText.ReminderKind(reminder.Kind),
                    at,
                    reminder.IsPaused,
                    reminder.CircleId));
            }
        }

        return items.OrderBy(x => x.At).ToList();
    }

    private async Task<string> EnsureFeedTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        if (string.IsNullOrEmpty(user.CalendarFeedToken))
        {
            user.CalendarFeedToken = TokenHash.NewToken().ToLowerInvariant();
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return user.CalendarFeedToken;
    }

    private static (DateTime FromUtc, DateTime ToUtc) Range(DateOnly? from, DateOnly? to, TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var start = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = to?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
            ?? start.AddMonths(1);
        return (start, end);
    }

    private static string ToIcs(IReadOnlyList<CalendarEventResponse> events, DateTime stamp) =>
        IcsCalendar.Build(
            "Lembretes do Circulos",
            events.Select(item => new IcsEvent(
                $"reminder-{item.ReminderId:N}-{IcsCalendar.Format(item.At)}@circulos",
                stamp,
                item.At,
                item.Title,
                item.Paused ? "Pausado. A data original da narrativa permanece." : null)).ToList());
}
