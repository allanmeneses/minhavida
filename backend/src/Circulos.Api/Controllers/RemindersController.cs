using System.Text.Json;
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
[Route("api/reminders")]
public sealed class RemindersController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReminderResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var now = clock.GetUtcNow().UtcDateTime;
        var items = await db.Reminders.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.NextFireAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToResponse(x, now)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ReminderResponse>> Create(
        [FromBody] ReminderRequest request,
        CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Utc);
        var rule = request.Recurrence ?? new RecurrenceRule();
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start, 0);
        if (next is null)
        {
            return BadRequest(new ErrorResponse("Esta recorrencia nao gera ocorrencias."));
        }

        Guid? circleId = null;
        if (request.CircleId is Guid requestedCircle)
        {
            var member = await db.CircleMembers.AnyAsync(
                x => x.CircleId == requestedCircle && x.UserId == User.GetUserId(),
                cancellationToken);
            if (!member)
            {
                return BadRequest(new ErrorResponse("Voce precisa participar do circulo."));
            }

            circleId = requestedCircle;
        }

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = User.GetUserId(),
            CircleId = circleId,
            Title = request.Title.Trim(),
            Kind = EnumText.ParseReminderKind(request.Kind),
            RecurrenceJson = rule.ToJson(),
            StartAt = start,
            NextFireAt = next.Value,
            UntilAt = rule.UntilUtc,
            MaxOccurrences = rule.MaxOccurrences,
            PersonName = request.PersonName?.Trim(),
            AdvanceDays = Math.Max(0, request.AdvanceDays),
            GiftIdeasJson = JsonSerializer.Serialize(request.GiftIdeas ?? []),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/reminders/{reminder.Id}", ToResponse(reminder, reminder.CreatedAt));
    }

    [HttpPost("{id:guid}/pause")]
    public Task<ActionResult<ReminderResponse>> Pause(Guid id, CancellationToken cancellationToken) =>
        SetPaused(id, true, cancellationToken);

    [HttpPost("{id:guid}/resume")]
    public Task<ActionResult<ReminderResponse>> Resume(Guid id, CancellationToken cancellationToken) =>
        SetPaused(id, false, cancellationToken);

    [HttpPost("{id:guid}/skip")]
    public async Task<ActionResult<ReminderResponse>> Skip(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await Owned(id, cancellationToken);
        if (reminder is null)
        {
            return NotFound(new ErrorResponse("Lembrete nao encontrado."));
        }

        var rule = RecurrenceRule.Parse(reminder.RecurrenceJson);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, reminder.StartAt, reminder.NextFireAt.AddSeconds(1), reminder.CompletedCount);
        if (next is null)
        {
            reminder.SkipNext = true;
        }
        else
        {
            reminder.NextFireAt = next.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(reminder, clock.GetUtcNow().UtcDateTime));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ReminderResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await Owned(id, cancellationToken);
        if (reminder is null)
        {
            return NotFound(new ErrorResponse("Lembrete nao encontrado."));
        }

        reminder.CompletedCount += 1;
        var rule = RecurrenceRule.Parse(reminder.RecurrenceJson);
        var next = RecurrenceCalculator.NextOnOrAfter(rule, reminder.StartAt, reminder.NextFireAt.AddSeconds(1), reminder.CompletedCount);
        if (next is null)
        {
            reminder.IsPaused = true;
        }
        else
        {
            reminder.NextFireAt = next.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(reminder, clock.GetUtcNow().UtcDateTime));
    }

    [HttpPut("vacation")]
    public async Task<IActionResult> Vacation([FromBody] VacationRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstAsync(x => x.Id == User.GetUserId(), cancellationToken);
        user.VacationUntil = request.Until is null ? null : DateTime.SpecifyKind(request.Until.Value, DateTimeKind.Utc);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("email-preference")]
    public async Task<ActionResult<EmailPreferenceResponse>> EmailPreference(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(x => x.Id == User.GetUserId(), cancellationToken);
        return Ok(new EmailPreferenceResponse(user.EmailRemindersEnabled));
    }

    [HttpPut("email-preference")]
    public async Task<ActionResult<EmailPreferenceResponse>> SetEmailPreference(
        [FromBody] EmailPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        user.EmailRemindersEnabled = request.Enabled;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new EmailPreferenceResponse(user.EmailRemindersEnabled));
    }

    private async Task<ActionResult<ReminderResponse>> SetPaused(Guid id, bool paused, CancellationToken cancellationToken)
    {
        var reminder = await Owned(id, cancellationToken);
        if (reminder is null)
        {
            return NotFound(new ErrorResponse("Lembrete nao encontrado."));
        }

        reminder.IsPaused = paused;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(reminder, clock.GetUtcNow().UtcDateTime));
    }

    private Task<Reminder?> Owned(Guid id, CancellationToken cancellationToken) =>
        db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);

    private static ReminderResponse ToResponse(Reminder reminder, DateTime now)
    {
        var rule = RecurrenceRule.Parse(reminder.RecurrenceJson);
        var ideas = JsonSerializer.Deserialize<string[]>(reminder.GiftIdeasJson) ?? [];
        var endingSoon = !string.Equals(rule.Frequency, "none", StringComparison.OrdinalIgnoreCase)
            && (rule.MaxOccurrences is int max && max - reminder.CompletedCount <= 3
                || rule.UntilUtc is { } until && until <= now.AddDays(21));
        return new ReminderResponse(
            reminder.Id,
            reminder.Title,
            EnumText.ReminderKind(reminder.Kind),
            reminder.CircleId,
            rule,
            reminder.StartAt,
            reminder.NextFireAt,
            reminder.IsPaused,
            reminder.SkipNext,
            reminder.PersonName,
            reminder.AdvanceDays,
            ideas,
            endingSoon);
    }
}
