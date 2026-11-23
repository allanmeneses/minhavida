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
public sealed class HomeController(
    AppDbContext db,
    TimeProvider clock,
    BillingService billing,
    ReminderEmailService reminderEmails,
    CircleArchiveService archive,
    UsageStats stats) : ControllerBase
{
    [HttpGet("home")]
    public async Task<ActionResult<DayPanelResponse>> Home(CancellationToken cancellationToken)
    {
        await archive.ArchiveExpiredAsync(cancellationToken);
        var userId = User.GetUserId();
        var emailed = await reminderEmails.DispatchDueAsync(userId, cancellationToken);
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

        var shortcuts = await VisibleShortcutsAsync(userId, cancellationToken);
        var notices = await db.ShortcutChangeNotices.AsNoTracking()
            .Where(x => x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new ShortcutNoticeResponse(x.Id, x.ShortcutId, x.Detail, x.CreatedAt))
            .ToListAsync(cancellationToken);

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
            plan,
            notices,
            emailed,
            user.EmailRemindersEnabled));
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
        var userId = User.GetUserId();
        var own = await db.Shortcuts.AsNoTracking()
            .Where(x => x.UserId == userId && (includeDeleted || !x.IsDeleted))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        return Ok(own.Select(item => ToShortcut(item)).ToList());
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
        var userId = User.GetUserId();
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(
            x => x.Id == id && x.IsActive && !x.IsDeleted,
            cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        var sharedCircleIds = await (
            from share in db.ShortcutShares
            join member in db.CircleMembers on share.CircleId equals member.CircleId
            where share.ShortcutId == id && member.UserId == userId
            select share.CircleId).Distinct().ToListAsync(cancellationToken);
        if (shortcut.UserId != userId && sharedCircleIds.Count == 0)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, shortcut.UserId, cancellationToken);
        shortcut = await db.Shortcuts.FirstOrDefaultAsync(
            x => x.Id == id && x.IsActive && !x.IsDeleted,
            cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        var owner = await db.Users.FirstAsync(x => x.Id == shortcut.UserId, cancellationToken);
        if (!PlanLimits.BenefitsActive(owner, clock.GetUtcNow().UtcDateTime))
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

        var now = clock.GetUtcNow().UtcDateTime;
        if (diary is not null)
        {
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

        if (sharedCircleIds.Count > 0)
        {
            var runner = await db.Profiles.AsNoTracking().FirstAsync(x => x.UserId == userId, cancellationToken);
            foreach (var circleId in sharedCircleIds)
            {
                db.CircleLogs.Add(new CircleLog
                {
                    Id = Guid.NewGuid(),
                    CircleId = circleId,
                    ActorUserId = userId,
                    Action = "shortcut-run",
                    Detail = $"{runner.DisplayName} disparou {shortcut.Label}.",
                    CreatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await stats.IncrementAsync(UsageStats.ShortcutRun, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { received = true, label = shortcut.Label });
    }

    [HttpPut("shortcuts/{id:guid}")]
    public async Task<ActionResult<ShortcutResponse>> UpdateShortcut(
        Guid id,
        [FromBody] ShortcutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        if (shortcut.UserId != userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So quem criou o atalho pode editar."));
        }

        var kind = string.IsNullOrWhiteSpace(request.ActionKind)
            ? shortcut.ActionKind
            : ShortcutPalette.Parse(request.ActionKind);
        var color = string.IsNullOrWhiteSpace(request.Color) ? shortcut.Color : request.Color;
        var label = request.Label.Trim();
        Guid? reminderId = shortcut.ReminderId;
        if (request.ReminderId is Guid linkedReminder)
        {
            var exists = await db.Reminders.AnyAsync(x => x.Id == linkedReminder && x.UserId == userId, cancellationToken);
            if (!exists)
            {
                return BadRequest(new ErrorResponse("Lembrete nao encontrado."));
            }

            reminderId = linkedReminder;
        }

        var changes = new List<string>();
        if (!string.Equals(shortcut.Label, label, StringComparison.Ordinal))
        {
            changes.Add($"Nome: {shortcut.Label} -> {label}");
        }

        if (!string.Equals(shortcut.Color, color, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add($"Cor: {shortcut.Color} -> {color}");
        }

        if (shortcut.ActionKind != kind)
        {
            changes.Add($"Tipo: {ShortcutPalette.Text(shortcut.ActionKind)} -> {ShortcutPalette.Text(kind)}");
        }

        if (shortcut.ReminderId != reminderId)
        {
            changes.Add("Lembrete ligado mudou.");
        }

        shortcut.Label = label;
        shortcut.Color = color;
        shortcut.ActionKind = kind;
        shortcut.ReminderId = reminderId;
        shortcut.SortOrder = request.SortOrder;
        var sharedCircleIds = await db.ShortcutShares.AsNoTracking()
            .Where(x => x.ShortcutId == shortcut.Id)
            .Select(x => x.CircleId)
            .ToListAsync(cancellationToken);
        if (changes.Count > 0 && sharedCircleIds.Count > 0)
        {
            var recipients = await db.CircleMembers.AsNoTracking()
                .Where(x => sharedCircleIds.Contains(x.CircleId) && x.UserId != userId)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var detail = string.Join(". ", changes);
            if (detail.Length > 500)
            {
                detail = detail[..500];
            }

            var stamped = clock.GetUtcNow().UtcDateTime;
            foreach (var recipient in recipients)
            {
                db.ShortcutChangeNotices.Add(new ShortcutChangeNotice
                {
                    Id = Guid.NewGuid(),
                    ShortcutId = shortcut.Id,
                    RecipientUserId = recipient,
                    Detail = detail,
                    CreatedAt = stamped,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToShortcut(shortcut));
    }

    [HttpPost("shortcuts/{id:guid}/share")]
    public async Task<ActionResult<ShortcutResponse>> ShareShortcut(
        Guid id,
        [FromBody] ShareShortcutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var shortcut = await db.Shortcuts.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId && !x.IsDeleted,
            cancellationToken);
        if (shortcut is null)
        {
            return NotFound(new ErrorResponse("Atalho nao encontrado."));
        }

        var member = await db.CircleMembers.AnyAsync(
            x => x.CircleId == request.CircleId && x.UserId == userId && x.Circle.Status == CircleStatus.Active,
            cancellationToken);
        if (!member)
        {
            return BadRequest(new ErrorResponse("Voce precisa participar do circulo."));
        }

        var already = await db.ShortcutShares.AnyAsync(
            x => x.ShortcutId == shortcut.Id && x.CircleId == request.CircleId,
            cancellationToken);
        if (!already)
        {
            db.ShortcutShares.Add(new ShortcutShare
            {
                Id = Guid.NewGuid(),
                ShortcutId = shortcut.Id,
                CircleId = request.CircleId,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(ToShortcut(shortcut, userId, null, request.CircleId));
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

    private async Task<List<ShortcutResponse>> VisibleShortcutsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var own = await db.Shortcuts.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var sharedRows = await (
            from share in db.ShortcutShares.AsNoTracking()
            join member in db.CircleMembers.AsNoTracking() on share.CircleId equals member.CircleId
            join shortcut in db.Shortcuts.AsNoTracking() on share.ShortcutId equals shortcut.Id
            where member.UserId == userId && shortcut.IsActive && !shortcut.IsDeleted && shortcut.UserId != userId
            select new { shortcut, share.CircleId, shortcut.UserId }).ToListAsync(cancellationToken);
        var ownerIds = sharedRows.Select(x => x.UserId).Distinct().ToList();
        var names = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Profiles.AsNoTracking()
                .Where(x => ownerIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId, x => x.DisplayName, cancellationToken);
        var result = own.Select(item => ToShortcut(item)).ToList();
        foreach (var row in sharedRows.GroupBy(item => item.shortcut.Id).Select(group => group.First()))
        {
            names.TryGetValue(row.UserId, out var name);
            result.Add(ToShortcut(row.shortcut, row.UserId, name, row.CircleId));
        }

        return result;
    }

    private static ShortcutResponse ToShortcut(
        Shortcut shortcut,
        Guid? sharedByUserId = null,
        string? sharedByName = null,
        Guid? circleId = null) =>
        new(
            shortcut.Id,
            shortcut.Label,
            shortcut.Color,
            shortcut.SortOrder,
            shortcut.IsActive,
            shortcut.ReminderId,
            ShortcutPalette.Text(shortcut.ActionKind),
            shortcut.IsDeleted,
            sharedByUserId,
            sharedByName,
            circleId);
}

