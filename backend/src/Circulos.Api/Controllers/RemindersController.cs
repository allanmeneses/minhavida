using System.Text.Json;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Reminders;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reminders")]
public sealed class RemindersController(AppDbContext db, TimeProvider clock, IFileStorage storage, QuotaService quota) : ControllerBase
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
        await using var transaction = request.CircleId is Guid
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (request.CircleId is Guid requestedCircle)
        {
            await CircleRowLock.AcquireAsync(db, requestedCircle, cancellationToken);
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == requestedCircle, cancellationToken);
            var member = circle?.Members.FirstOrDefault(x => x.UserId == User.GetUserId());
            if (circle is null || member is null)
            {
                return BadRequest(new ErrorResponse("Voce precisa participar do circulo."));
            }

            if (!CircleAccess.IsWritable(circle))
            {
                return BadRequest(new ErrorResponse(CircleAccess.LockedMessage));
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
        if (circleId is Guid shareCircle && (request.GiftIdeas?.Length ?? 0) > 0)
        {
            db.GiftIdeaShares.Add(new GiftIdeaShare
            {
                Id = Guid.NewGuid(),
                ReminderId = reminder.Id,
                CircleId = shareCircle,
                CreatedAt = reminder.CreatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

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
        var owner = await db.Users.FirstAsync(x => x.Id == reminder.UserId, cancellationToken);
        StreakService.Touch(owner, clock.GetUtcNow().UtcDateTime);
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
            endingSoon,
            reminder.PhotoStorageKey is not null);
    }

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<ReminderResponse>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var reminder = await Owned(id, cancellationToken);
        if (reminder is null)
        {
            return NotFound(new ErrorResponse("Lembrete nao encontrado."));
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse("Envie uma foto."));
        }

        var fileName = FileRules.SafeFileName(file.FileName);
        var error = FileRules.ValidatePhoto(fileName, file.ContentType, file.Length);
        if (error is not null)
        {
            return BadRequest(new ErrorResponse(error));
        }

        await using var original = file.OpenReadStream();
        await using var compressed = await ImageCompressor.CompressAsync(original, cancellationToken);
        var quotaError = await quota.TryAddStorageAsync(reminder.UserId, compressed.SizeBytes, cancellationToken);
        if (quotaError is not null)
        {
            return BadRequest(new ErrorResponse(quotaError));
        }

        var key = $"{reminder.UserId:N}/reminders/{reminder.Id:N}.jpg";
        await storage.SaveAsync(key, compressed.Content, cancellationToken);
        if (reminder.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(reminder.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(reminder.UserId, reminder.PhotoSizeBytes, cancellationToken);
        }

        reminder.PhotoStorageKey = key;
        reminder.PhotoContentType = compressed.ContentType;
        reminder.PhotoSizeBytes = compressed.SizeBytes;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(reminder, clock.GetUtcNow().UtcDateTime));
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await Owned(id, cancellationToken);
        if (reminder is null || reminder.PhotoStorageKey is null || reminder.PhotoContentType is null)
        {
            return NotFound(new ErrorResponse("Este lembrete ainda nao tem foto."));
        }

        var stream = await storage.OpenReadAsync(reminder.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, reminder.PhotoContentType, "lembrete.jpg");
    }

    [HttpPost("{id:guid}/share-ideas")]
    public async Task<IActionResult> ShareIdeas(
        Guid id,
        [FromBody] ShareGiftIdeasRequest request,
        CancellationToken cancellationToken)
    {
        var reminder = await Owned(id, cancellationToken);
        if (reminder is null)
        {
            return NotFound(new ErrorResponse("Lembrete nao encontrado."));
        }

        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var member = await db.CircleMembers.AnyAsync(
            x => x.CircleId == request.CircleId && x.UserId == userId && x.Circle.Status == CircleStatus.Active,
            cancellationToken);
        if (!member)
        {
            return BadRequest(new ErrorResponse("Voce precisa participar do circulo."));
        }

        var already = await db.GiftIdeaShares.AnyAsync(
            x => x.ReminderId == reminder.Id && x.CircleId == request.CircleId,
            cancellationToken);
        if (!already)
        {
            db.GiftIdeaShares.Add(new GiftIdeaShare
            {
                Id = Guid.NewGuid(),
                ReminderId = reminder.Id,
                CircleId = request.CircleId,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}
