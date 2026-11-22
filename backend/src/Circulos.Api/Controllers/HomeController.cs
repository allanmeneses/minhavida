using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class HomeController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("home")]
    public async Task<ActionResult<DayPanelResponse>> Home(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var onVacation = user.VacationUntil is { } until && until > now;

        var reminders = await db.Reminders.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsPaused)
            .ToListAsync(cancellationToken);

        var overdue = new List<DayPanelItem>();
        var dueToday = new List<DayPanelItem>();
        var upcoming = new List<DayPanelItem>();
        if (!onVacation)
        {
            foreach (var reminder in reminders)
            {
                var fireAt = reminder.Kind == ReminderKind.Birthday
                    ? reminder.NextFireAt.AddDays(-reminder.AdvanceDays)
                    : reminder.NextFireAt;
                var rule = RecurrenceRule.Parse(reminder.RecurrenceJson);
                if (fireAt < now && !RecurrenceCalculator.StillRelevant(rule, reminder.StartAt, reminder.NextFireAt, now))
                {
                    continue;
                }

                var item = new DayPanelItem(reminder.Id, reminder.Title, EnumText.ReminderKind(reminder.Kind), fireAt, fireAt < now);
                if (fireAt < now)
                {
                    overdue.Add(item);
                }
                else if (fireAt.Date == now.Date)
                {
                    dueToday.Add(item);
                }
                else
                {
                    upcoming.Add(item);
                }
            }
        }

        var shortcuts = await db.Shortcuts.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ShortcutResponse(x.Id, x.Label, x.Color, x.SortOrder, x.IsActive, x.ReminderId))
            .ToListAsync(cancellationToken);

        var circles = await db.CircleMembers.CountAsync(
            x => x.UserId == userId && x.Circle.Status == CircleStatus.Active,
            cancellationToken);

        var plan = ToPlan(user);
        return Ok(new DayPanelResponse(
            overdue.OrderBy(x => x.At).ToList(),
            dueToday.OrderBy(x => x.At).ToList(),
            upcoming.OrderBy(x => x.At).Take(10).ToList(),
            circles,
            shortcuts,
            user.VacationUntil,
            plan));
    }

    [HttpGet("plan")]
    public async Task<ActionResult<PlanResponse>> Plan(CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstAsync(x => x.Id == User.GetUserId(), cancellationToken);
        return Ok(ToPlan(user));
    }

    [HttpPost("plan/checkout")]
    public IActionResult Checkout() =>
        StatusCode(StatusCodes.Status501NotImplemented, new ErrorResponse("Cobranca recorrente ainda nao esta ligada a um gateway."));

    [HttpGet("shortcuts")]
    public async Task<ActionResult<IReadOnlyList<ShortcutResponse>>> Shortcuts(CancellationToken cancellationToken)
    {
        var items = await db.Shortcuts.AsNoTracking()
            .Where(x => x.UserId == User.GetUserId())
            .OrderBy(x => x.SortOrder)
            .Select(x => new ShortcutResponse(x.Id, x.Label, x.Color, x.SortOrder, x.IsActive, x.ReminderId))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost("shortcuts")]
    public async Task<ActionResult<ShortcutResponse>> CreateShortcut(
        [FromBody] ShortcutRequest request,
        CancellationToken cancellationToken)
    {
        Guid? reminderId = null;
        if (request.ReminderId is Guid linkedReminder)
        {
            var exists = await db.Reminders.AnyAsync(
                x => x.Id == linkedReminder && x.UserId == User.GetUserId(),
                cancellationToken);
            if (!exists)
            {
                return BadRequest(new ErrorResponse("Lembrete nao encontrado."));
            }

            reminderId = linkedReminder;
        }

        var shortcut = new Shortcut
        {
            Id = Guid.NewGuid(),
            UserId = User.GetUserId(),
            ReminderId = reminderId,
            Label = request.Label.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#1F6F66" : request.Color,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Shortcuts.Add(shortcut);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/shortcuts/{shortcut.Id}", new ShortcutResponse(shortcut.Id, shortcut.Label, shortcut.Color, shortcut.SortOrder, shortcut.IsActive, shortcut.ReminderId));
    }

    [HttpPost("shortcuts/{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == User.GetUserId() && x.IsActive,
            cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        var diary = await db.LifeAreas.FirstOrDefaultAsync(
            x => x.UserId == shortcut.UserId && x.Kind == LifeAreaKind.Diary,
            cancellationToken);
        if (shortcut.ReminderId is Guid reminderId)
        {
            var reminder = await db.Reminders.FirstOrDefaultAsync(
                x => x.Id == reminderId && x.UserId == shortcut.UserId,
                cancellationToken);
            if (reminder is not null)
            {
                reminder.CompletedCount += 1;
                var rule = RecurrenceRule.Parse(reminder.RecurrenceJson);
                var next = RecurrenceCalculator.NextOnOrAfter(
                    rule,
                    reminder.StartAt,
                    reminder.NextFireAt.AddSeconds(1),
                    reminder.CompletedCount);
                if (next is null)
                {
                    reminder.IsPaused = true;
                }
                else
                {
                    reminder.NextFireAt = next.Value;
                }
            }
        }

        if (diary is not null)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            db.LifeEntries.Add(new LifeEntry
            {
                Id = Guid.NewGuid(),
                LifeAreaId = diary.Id,
                UserId = shortcut.UserId,
                Title = shortcut.Label,
                Body = "Registrado pelo atalho.",
                OccurredOn = DateOnly.FromDateTime(now),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { received = true, label = shortcut.Label });
    }

    [HttpPost("shortcuts/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        shortcut.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("shortcuts/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        shortcut.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("shortcuts/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        db.Shortcuts.Remove(shortcut);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("shortcuts/order")]
    public async Task<IActionResult> Reorder([FromBody] ReorderShortcutsRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var items = await db.Shortcuts.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        for (var i = 0; i < request.Ids.Length; i++)
        {
            var item = items.FirstOrDefault(x => x.Id == request.Ids[i]);
            if (item is not null)
            {
                item.SortOrder = i;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static PlanResponse ToPlan(User user)
    {
        var limit = PlanLimits.StorageLimit(user.Plan);
        return new PlanResponse(
            EnumText.Plan(user.Plan),
            PlanLimits.CircleLimit(user.Plan),
            user.OwnedCircleCount,
            limit,
            user.StorageUsedBytes,
            PlanLimits.ShouldWarn(user.StorageUsedBytes, limit),
            "Cobranca recorrente ainda nao esta ligada a um gateway.");
    }
}
