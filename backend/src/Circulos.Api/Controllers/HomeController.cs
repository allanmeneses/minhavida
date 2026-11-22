using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Reminders;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class HomeController(AppDbContext db, TimeProvider clock, BillingService billing) : ControllerBase
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

        var shortcuts = (await db.Shortcuts.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken))
            .Select(ToShortcut)
            .ToList();

        var circles = await db.CircleMembers.CountAsync(
            x => x.UserId == userId && x.Circle.Status == CircleStatus.Active,
            cancellationToken);

        var plan = billing.ToPlan(user);
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
        return Ok(billing.ToPlan(user));
    }

    [HttpGet("shortcuts")]
    public async Task<ActionResult<IReadOnlyList<ShortcutResponse>>> Shortcuts(CancellationToken cancellationToken)
    {
        var includeDeleted = string.Equals(HttpContext.Request.Query["includeDeleted"], "true", StringComparison.OrdinalIgnoreCase);
        var items = (await db.Shortcuts.AsNoTracking()
            .Where(x => x.UserId == User.GetUserId() && (includeDeleted || !x.IsDeleted))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken))
            .Select(ToShortcut)
            .ToList();
        return Ok(items);
    }

    [HttpPost("shortcuts")]
    public async Task<ActionResult<ShortcutResponse>> CreateShortcut(
        [FromBody] ShortcutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var paid = PlanLimits.BenefitsActive(user, clock.GetUtcNow().UtcDateTime);
        var limit = PlanLimits.ShortcutLimit(paid);
        var count = await db.Shortcuts.CountAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);
        if (count >= limit)
        {
            return BadRequest(new ErrorResponse(paid
                ? "O plano pago inclui ate 20 atalhos."
                : "O plano gratuito inclui ate 5 atalhos."));
        }

        Guid? reminderId = null;
        if (request.ReminderId is Guid linkedReminder)
        {
            var exists = await db.Reminders.AnyAsync(
                x => x.Id == linkedReminder && x.UserId == userId,
                cancellationToken);
            if (!exists)
            {
                return BadRequest(new ErrorResponse("Lembrete nao encontrado."));
            }

            reminderId = linkedReminder;
        }

        var kind = ShortcutPalette.Parse(request.ActionKind);
        var shortcut = new Shortcut
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReminderId = reminderId,
            Label = request.Label.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? ShortcutPalette.ColorFor(kind) : request.Color,
            ActionKind = kind,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Shortcuts.Add(shortcut);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/shortcuts/{shortcut.Id}", ToShortcut(shortcut));
    }

    [HttpPost("shortcuts/{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == User.GetUserId() && x.IsActive && !x.IsDeleted,
            cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        var user = await db.Users.FirstAsync(x => x.Id == shortcut.UserId, cancellationToken);
        if (!PlanLimits.BenefitsActive(user, clock.GetUtcNow().UtcDateTime))
        {
            var allowed = await db.Shortcuts.AsNoTracking()
                .Where(x => x.UserId == shortcut.UserId && x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Id)
                .Take(PlanLimits.FreeShortcutLimit)
                .ToListAsync(cancellationToken);
            if (!allowed.Contains(shortcut.Id))
            {
                return StatusCode(StatusCodes.Status402PaymentRequired, new ErrorResponse("Assinatura suspensa. Atalhos extras ficam bloqueados."));
            }
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
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId() && !x.IsDeleted, cancellationToken);
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
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId() && !x.IsDeleted, cancellationToken);
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
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId() && !x.IsDeleted, cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        shortcut.IsDeleted = true;
        shortcut.IsActive = false;
        shortcut.DeletedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("shortcuts/{id:guid}/restore")]
    public async Task<ActionResult<ShortcutResponse>> Restore(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.IsDeleted, cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var paid = PlanLimits.BenefitsActive(user, clock.GetUtcNow().UtcDateTime);
        var limit = PlanLimits.ShortcutLimit(paid);
        var count = await db.Shortcuts.CountAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);
        if (count >= limit)
        {
            return BadRequest(new ErrorResponse("Nao ha vaga no teto de atalhos deste plano."));
        }

        shortcut.IsDeleted = false;
        shortcut.DeletedAt = null;
        shortcut.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToShortcut(shortcut));
    }

    [HttpPut("shortcuts/order")]
    public async Task<IActionResult> Reorder([FromBody] ReorderShortcutsRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var items = await db.Shortcuts.Where(x => x.UserId == userId && !x.IsDeleted).ToListAsync(cancellationToken);
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

    private static ShortcutResponse ToShortcut(Shortcut shortcut) =>
        new(
            shortcut.Id,
            shortcut.Label,
            shortcut.Color,
            shortcut.SortOrder,
            shortcut.IsActive,
            shortcut.ReminderId,
            ShortcutPalette.Text(shortcut.ActionKind),
            shortcut.IsDeleted);
}

