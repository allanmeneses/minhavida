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
[Route("api")]
public sealed class RecordsController(
    AppDbContext db,
    TimeProvider clock,
    IRecordSuggester suggester,
    ITranscriber transcriber,
    IFileStorage storage,
    QuotaService quota,
    RecordMigrationService migration,
    UsageStats stats) : ControllerBase
{
    [HttpGet("record-types")]
    public async Task<ActionResult<IReadOnlyList<RecordTypeResponse>>> ListTypes(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        await migration.MigrateUserAsync(userId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var types = await db.RecordTypes.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.LastUsedAt)
            .ThenBy(item => item.Name)
            .Select(item => new { Type = item, Count = item.Records.Count })
            .ToListAsync(cancellationToken);
        return Ok(types.Select(item => ToType(item.Type, item.Count)).ToList());
    }

    [HttpGet("record-types/gallery")]
    public async Task<ActionResult<IReadOnlyList<RecordTypeResponse>>> Gallery(CancellationToken cancellationToken)
    {
        await RecordGallery.EnsureSeededAsync(db, cancellationToken);
        var items = await db.RecordTypes.AsNoTracking()
            .Where(item => item.UserId == null)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(item => ToType(item, 0)).ToList());
    }

    [HttpPost("record-types/suggest")]
    public async Task<ActionResult<RecordTypeSuggestionDto>> SuggestType(
        [FromBody] SentenceTypeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await RecordGallery.EnsureSeededAsync(db, cancellationToken);
        var existing = await db.RecordTypes.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var gallery = await db.RecordTypes.AsNoTracking().Where(item => item.UserId == null).ToListAsync(cancellationToken);
        var suggestion = suggester.SuggestType(request.Sentence, existing, gallery);
        return Ok(ToDto(suggestion));
    }

    [HttpPost("record-types")]
    public async Task<ActionResult<RecordTypeResponse>> CreateType(
        [FromBody] CreateRecordTypeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var type = BuildType(userId, request, now);
        db.RecordTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/record-types/{type.Id}", ToType(type, 0));
    }

    [HttpPost("record-types/from-gallery")]
    public async Task<ActionResult<RecordTypeResponse>> CopyGallery(
        [FromBody] CopyGalleryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        await RecordGallery.EnsureSeededAsync(db, cancellationToken);
        var template = await db.RecordTypes.FirstOrDefaultAsync(
            item => item.Id == request.TemplateId && item.UserId == null,
            cancellationToken);
        if (template is null)
        {
            return NotFound(new ErrorResponse("Este exemplo não está na galeria."));
        }

        var existing = await db.RecordTypes.FirstOrDefaultAsync(
            item => item.UserId == userId && item.OriginTemplateId == template.Id,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            var count = await db.LifeRecords.CountAsync(item => item.TypeId == existing.Id, cancellationToken);
            return Ok(ToType(existing, count));
        }

        var copy = new RecordType
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = template.Name,
            Description = template.Description,
            Slug = template.Slug,
            SchemaJson = template.SchemaJson,
            OriginTemplateId = template.Id,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.RecordTypes.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/record-types/{copy.Id}", ToType(copy, 0));
    }

    [HttpGet("record-types/{id:guid}")]
    public async Task<ActionResult<RecordTypeResponse>> GetType(Guid id, CancellationToken cancellationToken)
    {
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var count = await db.LifeRecords.CountAsync(item => item.TypeId == type.Id, cancellationToken);
        return Ok(ToType(type, count));
    }

    [HttpPut("record-types/{id:guid}")]
    public async Task<ActionResult<RecordTypeResponse>> UpdateType(
        Guid id,
        [FromBody] CreateRecordTypeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        type.Name = request.Name.Trim();
        type.Description = request.Description?.Trim() ?? type.Description;
        type.SchemaJson = SchemaFrom(request.Fields).ToJson();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var count = await db.LifeRecords.CountAsync(item => item.TypeId == type.Id, cancellationToken);
        return Ok(ToType(type, count));
    }

    [HttpPost("record-types/{id:guid}/fields")]
    public async Task<ActionResult<RecordTypeResponse>> AddField(
        Guid id,
        [FromBody] AddRecordFieldRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var schema = RecordSchema.Parse(type.SchemaJson);
        var fieldId = RuleBasedRecordSuggester.Slugify(request.Name);
        if (schema.Field(fieldId) is not null)
        {
            fieldId = $"{fieldId}-{schema.Fields.Count + 1}";
        }

        schema.Fields.Add(RecordField.Create(fieldId, request.Name.Trim(), request.Kind, schema.Fields.Count, request.Required));
        type.SchemaJson = schema.ToJson();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var count = await db.LifeRecords.CountAsync(item => item.TypeId == type.Id, cancellationToken);
        return Ok(ToType(type, count));
    }

    [HttpGet("record-types/{id:guid}/records")]
    public async Task<ActionResult<IReadOnlyList<LifeRecordResponse>>> ListRecords(Guid id, CancellationToken cancellationToken)
    {
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var items = await db.LifeRecords.AsNoTracking()
            .Include(item => item.Shares)
            .Where(item => item.TypeId == id && item.UserId == User.GetUserId())
            .OrderByDescending(item => item.OccurredOn)
            .ThenByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(item => ToRecord(item, type.Name)).ToList());
    }

    [HttpGet("record-types/{id:guid}/calendar")]
    public async Task<ActionResult<IReadOnlyList<RecordCalendarItem>>> Calendar(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var start = from ?? new DateOnly(2000, 1, 1);
        var end = to ?? new DateOnly(2100, 1, 1);
        var items = await db.LifeRecords.AsNoTracking()
            .Where(item => item.TypeId == id && item.UserId == User.GetUserId() && item.OccurredOn != null && item.OccurredOn >= start && item.OccurredOn <= end)
            .OrderBy(item => item.OccurredOn)
            .Select(item => new RecordCalendarItem(item.Id, item.Title, item.OccurredOn!.Value, item.NumberValue))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("record-types/{id:guid}/chart")]
    public async Task<ActionResult<IReadOnlyList<RecordChartPoint>>> Chart(Guid id, CancellationToken cancellationToken)
    {
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var schema = RecordSchema.Parse(type.SchemaJson);
        if (schema.OrderedFields().All(field => field.Kind != RecordFieldKind.Number))
        {
            return Ok(Array.Empty<RecordChartPoint>());
        }

        var points = await db.LifeRecords.AsNoTracking()
            .Where(item => item.TypeId == id && item.UserId == User.GetUserId() && item.NumberValue != null)
            .OrderBy(item => item.OccurredOn)
            .Select(item => new RecordChartPoint(item.OccurredOn, item.NumberValue!.Value))
            .ToListAsync(cancellationToken);
        return Ok(points);
    }

    [HttpGet("record-types/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var type = await OwnedType(id, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var items = await db.LifeRecords.AsNoTracking()
            .Where(item => item.TypeId == id && item.UserId == User.GetUserId())
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var text = new System.Text.StringBuilder();
        text.AppendLine(type.Name);
        foreach (var item in items)
        {
            text.AppendLine();
            text.AppendLine(item.Title);
            if (!string.IsNullOrWhiteSpace(item.Body))
            {
                text.AppendLine(item.Body);
            }
        }

        return File(System.Text.Encoding.UTF8.GetBytes(text.ToString()), "text/plain; charset=utf-8", $"{type.Slug}.txt");
    }

    [HttpPost("records/suggest")]
    public async Task<ActionResult<RecordSuggestionResponse>> Suggest(
        [FromBody] SuggestRecordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await RecordGallery.EnsureSeededAsync(db, cancellationToken);
        var existing = await db.RecordTypes.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var gallery = await db.RecordTypes.AsNoTracking().Where(item => item.UserId == null).ToListAsync(cancellationToken);
        var suggestion = await SuggestAsync(request.Text, existing, gallery, cancellationToken);
        return Ok(ToSuggestion(suggestion, null));
    }

    [HttpPost("records/transcribe")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<RecordSuggestionResponse>> Transcribe(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse("Envie um áudio."));
        }

        if (!transcriber.IsAvailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("A transcrição de áudio não está disponível agora."));
        }

        var fileName = FileRules.SafeFileName(file.FileName);
        var error = FileRules.ValidateAudio(fileName, file.ContentType, file.Length);
        if (error is not null)
        {
            return BadRequest(new ErrorResponse(error));
        }

        var userId = User.GetUserId();
        var key = $"{userId:N}/audio/{Guid.NewGuid():N}-{fileName}";
        await using (var incoming = file.OpenReadStream())
        {
            await storage.SaveAsync(key, incoming, cancellationToken);
        }

        try
        {
            await using var stored = await storage.OpenReadAsync(key, cancellationToken);
            var result = await transcriber.TranscribeAsync(stored, fileName, file.ContentType ?? "application/octet-stream", cancellationToken);
            if (!result.Available || string.IsNullOrWhiteSpace(result.Text))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse(result.Error ?? "A transcrição de áudio não está disponível agora."));
            }

            await RecordGallery.EnsureSeededAsync(db, cancellationToken);
            var existing = await db.RecordTypes.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(cancellationToken);
            var gallery = await db.RecordTypes.AsNoTracking().Where(item => item.UserId == null).ToListAsync(cancellationToken);
            var suggestion = await SuggestAsync(result.Text, existing, gallery, cancellationToken);
            return Ok(ToSuggestion(suggestion, result.Text));
        }
        finally
        {
            await storage.DeleteAsync(key, cancellationToken);
        }
    }

    [HttpPost("records")]
    public async Task<ActionResult<LifeRecordResponse>> Create(
        [FromBody] CreateRecordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        RecordType? type;
        if (request.CreateTypeIfMissing && request.NewType is not null)
        {
            type = BuildType(userId, request.NewType, now);
            db.RecordTypes.Add(type);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (request.TypeId is Guid typeId)
        {
            type = await OwnedType(typeId, cancellationToken);
            if (type is null)
            {
                return NotFound(new ErrorResponse("Tipo não encontrado."));
            }
        }
        else
        {
            return BadRequest(new ErrorResponse("Escolha um tipo ou descreva um novo."));
        }

        var schema = RecordSchema.Parse(type.SchemaJson);
        var fields = request.Fields ?? new Dictionary<string, JsonElement>();
        var record = new LifeRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TypeId = type.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        RecordFields.ApplyIndexed(record, schema, fields);
        db.LifeRecords.Add(record);
        type.LastUsedAt = now;
        var owner = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        StreakService.Touch(owner, now);
        await db.SaveChangesAsync(cancellationToken);
        await stats.IncrementAsync(UsageStats.EntryCreated, cancellationToken);
        if (request.CircleIds is { Length: > 0 })
        {
            var shareError = await ReplaceSharesAsync(record, request.CircleIds, userId, now, cancellationToken);
            if (shareError is not null)
            {
                return BadRequest(new ErrorResponse(shareError));
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await db.Entry(record).Collection(item => item.Shares).LoadAsync(cancellationToken);
        return Created($"/api/records/{record.Id}", ToRecord(record, type.Name));
    }

    [HttpPut("records/{id:guid}")]
    public async Task<ActionResult<LifeRecordResponse>> Update(
        Guid id,
        [FromBody] UpdateRecordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var record = await OwnedRecord(id, cancellationToken);
        if (record is null)
        {
            return NotFound(new ErrorResponse("Registro não encontrado."));
        }

        var type = await OwnedType(record.TypeId, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        if (request.Fields is not null)
        {
            RecordFields.ApplyIndexed(record, RecordSchema.Parse(type.SchemaJson), request.Fields);
        }

        record.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        type.LastUsedAt = record.UpdatedAt;
        if (request.CircleIds is not null)
        {
            var shareError = await ReplaceSharesAsync(record, request.CircleIds, userId, record.UpdatedAt, cancellationToken);
            if (shareError is not null)
            {
                return BadRequest(new ErrorResponse(shareError));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(record).Collection(item => item.Shares).LoadAsync(cancellationToken);
        return Ok(ToRecord(record, type.Name));
    }

    [HttpPost("records/{id:guid}/share")]
    public async Task<ActionResult<LifeRecordResponse>> Share(
        Guid id,
        [FromBody] ShareEntryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var record = await OwnedRecord(id, cancellationToken);
        if (record is null)
        {
            return NotFound(new ErrorResponse("Registro não encontrado."));
        }

        var shareError = await ReplaceSharesAsync(record, request.CircleIds ?? [], userId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (shareError is not null)
        {
            return BadRequest(new ErrorResponse(shareError));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var type = await db.RecordTypes.AsNoTracking().FirstAsync(item => item.Id == record.TypeId, cancellationToken);
        await db.Entry(record).Collection(item => item.Shares).LoadAsync(cancellationToken);
        return Ok(ToRecord(record, type.Name));
    }

    [HttpPost("records/{id:guid}/remind")]
    public async Task<ActionResult<LifeRecordResponse>> Remind(
        Guid id,
        [FromBody] RecordRemindRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var record = await OwnedRecord(id, cancellationToken);
        if (record is null)
        {
            return NotFound(new ErrorResponse("Registro não encontrado."));
        }

        var type = await OwnedType(record.TypeId, cancellationToken);
        if (type is null)
        {
            return NotFound(new ErrorResponse("Tipo não encontrado."));
        }

        var schema = RecordSchema.Parse(type.SchemaJson);
        var fields = RecordFields.Parse(record.FieldsJson);
        var fieldId = request.FieldId;
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            fieldId = schema.OrderedFields().FirstOrDefault(field => field.Kind == RecordFieldKind.Date)?.Id;
        }

        var field = schema.Field(fieldId ?? string.Empty);
        if (field is null || field.Kind != RecordFieldKind.Date)
        {
            return BadRequest(new ErrorResponse("Este tipo não tem um campo de data para lembrar."));
        }

        var date = RecordFields.ReadDate(fields, field.Id) ?? record.OccurredOn;
        if (date is null)
        {
            return BadRequest(new ErrorResponse("Preencha a data antes de pedir o lembrete."));
        }

        var start = DateTime.SpecifyKind(date.Value.ToDateTime(new TimeOnly(8, 0)), DateTimeKind.Utc);
        var rule = new RecurrenceRule { Frequency = "none", Interval = 1 };
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start, 0);
        if (next is null)
        {
            return BadRequest(new ErrorResponse("Esta data não gera lembrete."));
        }

        if (record.ReminderId is Guid reminderId)
        {
            var reminder = await db.Reminders.FirstOrDefaultAsync(item => item.Id == reminderId && item.UserId == userId, cancellationToken);
            if (reminder is not null)
            {
                reminder.Title = record.Title;
                reminder.StartAt = start;
                reminder.NextFireAt = next.Value;
                reminder.RecurrenceJson = rule.ToJson();
            }
        }
        else
        {
            var reminder = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = record.Title,
                Kind = ReminderKind.General,
                RecurrenceJson = rule.ToJson(),
                StartAt = start,
                NextFireAt = next.Value,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            };
            db.Reminders.Add(reminder);
            record.ReminderId = reminder.Id;
        }

        record.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(record).Collection(item => item.Shares).LoadAsync(cancellationToken);
        return Ok(ToRecord(record, type.Name));
    }

    [HttpPost("records/{id:guid}/photo")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<LifeRecordResponse>> UploadPhoto(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        var record = await OwnedRecord(id, cancellationToken);
        if (record is null)
        {
            return NotFound(new ErrorResponse("Registro não encontrado."));
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
        var quotaError = await quota.TryAddStorageAsync(record.UserId, compressed.SizeBytes, cancellationToken);
        if (quotaError is not null)
        {
            return BadRequest(new ErrorResponse(quotaError));
        }

        var key = $"{record.UserId:N}/records/{record.Id:N}.jpg";
        await storage.SaveAsync(key, compressed.Content, cancellationToken);
        if (record.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(record.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(record.UserId, record.PhotoSizeBytes, cancellationToken);
        }

        record.PhotoStorageKey = key;
        record.PhotoContentType = compressed.ContentType;
        record.PhotoSizeBytes = compressed.SizeBytes;
        record.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        var type = await db.RecordTypes.AsNoTracking().FirstAsync(item => item.Id == record.TypeId, cancellationToken);
        await db.Entry(record).Collection(item => item.Shares).LoadAsync(cancellationToken);
        return Ok(ToRecord(record, type.Name));
    }

    [HttpGet("records/{id:guid}/photo")]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken cancellationToken)
    {
        var record = await OwnedRecord(id, cancellationToken);
        if (record is null || record.PhotoStorageKey is null || record.PhotoContentType is null)
        {
            return NotFound(new ErrorResponse("Este registro ainda não tem foto."));
        }

        var stream = await storage.OpenReadAsync(record.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, record.PhotoContentType, "registro.jpg");
    }

    private Task<RecordFillSuggestion> SuggestAsync(
        string text,
        IReadOnlyList<RecordType> existing,
        IReadOnlyList<RecordType> gallery,
        CancellationToken cancellationToken) =>
        suggester.SuggestRecordAsync(text, existing, gallery, cancellationToken);

    private Task<RecordType?> OwnedType(Guid id, CancellationToken cancellationToken) =>
        db.RecordTypes.FirstOrDefaultAsync(item => item.Id == id && item.UserId == User.GetUserId(), cancellationToken);

    private Task<LifeRecord?> OwnedRecord(Guid id, CancellationToken cancellationToken) =>
        db.LifeRecords.FirstOrDefaultAsync(item => item.Id == id && item.UserId == User.GetUserId(), cancellationToken);

    private async Task<string?> ReplaceSharesAsync(
        LifeRecord record,
        Guid[] circleIds,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var wanted = circleIds.Distinct().ToArray();
        if (wanted.Length > 0)
        {
            var allowed = await db.CircleMembers
                .AsNoTracking()
                .Where(item => item.UserId == userId && wanted.Contains(item.CircleId) && item.Circle.Status == CircleStatus.Active)
                .Select(item => item.CircleId)
                .ToListAsync(cancellationToken);
            if (allowed.Count != wanted.Length)
            {
                return "Você precisa participar do círculo para mostrar o registro lá.";
            }
        }

        var current = await db.RecordShares.Where(item => item.RecordId == record.Id).ToListAsync(cancellationToken);
        foreach (var share in current.Where(item => !wanted.Contains(item.CircleId)))
        {
            db.RecordShares.Remove(share);
        }

        var already = current.Select(item => item.CircleId).ToHashSet();
        foreach (var circleId in wanted.Where(item => !already.Contains(item)))
        {
            db.RecordShares.Add(new RecordShare
            {
                Id = Guid.NewGuid(),
                RecordId = record.Id,
                CircleId = circleId,
                CreatedAt = now,
            });
        }

        return null;
    }

    private static RecordType BuildType(Guid userId, CreateRecordTypeRequest request, DateTime now)
    {
        var schema = SchemaFrom(request.Fields);
        if (schema.Fields.Count == 0)
        {
            schema.Fields.Add(RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true));
        }

        return new RecordType
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Slug = RuleBasedRecordSuggester.Slugify(request.Name),
            SchemaJson = schema.ToJson(),
            CreatedAt = now,
        };
    }

    private static RecordSchema SchemaFrom(RecordFieldDto[]? fields)
    {
        var schema = new RecordSchema();
        if (fields is null)
        {
            return schema;
        }

        var order = 0;
        foreach (var field in fields)
        {
            var id = string.IsNullOrWhiteSpace(field.Id) ? RuleBasedRecordSuggester.Slugify(field.Name) : field.Id.Trim();
            schema.Fields.Add(RecordField.Create(id, field.Name.Trim(), field.Kind, field.Order == 0 ? order : field.Order, field.Required, field.Options));
            order++;
        }

        return schema;
    }

    private static RecordTypeResponse ToType(RecordType type, int count)
    {
        var schema = RecordSchema.Parse(type.SchemaJson);
        var fields = schema.OrderedFields().Select(ToFieldDto).ToList();
        var quick = schema.QuickFields().Select(ToFieldDto).ToList();
        return new RecordTypeResponse(
            type.Id,
            type.Name,
            type.Description,
            type.Slug,
            type.UserId is null,
            type.OriginTemplateId,
            fields,
            quick,
            type.CreatedAt,
            type.LastUsedAt,
            count,
            fields.Any(field => field.Kind == RecordFieldKind.Date),
            fields.Any(field => field.Kind == RecordFieldKind.Number));
    }

    private static RecordFieldDto ToFieldDto(RecordField field) =>
        new(field.Id, field.Name, field.Kind, field.Required, field.Order, field.Options);

    private static RecordFieldDto ToFieldDto(SuggestedField field) =>
        new(field.Id, field.Name, field.Kind, field.Required, field.Order, field.Options);

    private static LifeRecordResponse ToRecord(LifeRecord record, string typeName) =>
        new(
            record.Id,
            record.TypeId,
            typeName,
            record.Title,
            record.Body,
            record.OccurredOn,
            record.NumberValue,
            RecordFields.Parse(record.FieldsJson),
            record.PhotoStorageKey is not null,
            record.FileStorageKey is not null,
            record.FileName,
            record.ReminderId,
            record.Shares.Select(item => item.CircleId).ToList(),
            record.CreatedAt,
            record.UpdatedAt);

    private static RecordTypeSuggestionDto ToDto(RecordTypeSuggestion suggestion) =>
        new(
            suggestion.Name,
            suggestion.Description,
            suggestion.Slug,
            suggestion.GalleryTypeId,
            suggestion.ExistingTypeId,
            suggestion.Fields.Select(ToFieldDto).ToList());

    private static RecordSuggestionResponse ToSuggestion(RecordFillSuggestion suggestion, string? transcript) =>
        new(suggestion.TypeId, suggestion.IsNewType, ToDto(suggestion.Type), suggestion.Fields, suggestion.Summary, transcript);
}
