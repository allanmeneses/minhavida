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
[Route("api/templates")]
public sealed class TemplatesController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateResponse>>> List(CancellationToken cancellationToken)
    {
        await TemplateCatalog.EnsureSeededAsync(db, cancellationToken);
        var items = await db.RoutineTemplates.AsNoTracking()
            .OrderByDescending(x => x.UseCount)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(items.Select((item, index) => ToResponse(item, index + 1)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TemplateResponse>> Create(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var kind = EnumText.ParseTemplateKind(request.Kind);
        string payload;
        if (kind == RoutineTemplateKind.List)
        {
            if (request.ListId is not Guid listId)
            {
                return BadRequest(new ErrorResponse("Informe a lista para virar modelo."));
            }

            var list = await db.SharedLists.Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == listId && x.OwnerUserId == userId, cancellationToken);
            if (list is null)
            {
                return NotFound(new ErrorResponse("Lista nao encontrada."));
            }

            payload = TemplateCatalog.ListPayload(
                EnumText.ListKind(list.Kind),
                list.Items.OrderBy(x => x.SortOrder).Select(x => (x.Text, x.Quantity)).ToList());
        }
        else
        {
            if (request.ReminderId is not Guid reminderId)
            {
                return BadRequest(new ErrorResponse("Informe o lembrete para virar modelo."));
            }

            var reminder = await db.Reminders.FirstOrDefaultAsync(
                x => x.Id == reminderId && x.UserId == userId,
                cancellationToken);
            if (reminder is null)
            {
                return NotFound(new ErrorResponse("Lembrete nao encontrado."));
            }

            payload = TemplateCatalog.ReminderPayload(
                reminder.Title,
                EnumText.ReminderKind(reminder.Kind),
                RecurrenceRule.Parse(reminder.RecurrenceJson),
                reminder.PersonName);
        }

        var template = new RoutineTemplate
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            PayloadJson = payload,
            Curated = false,
            CreatorUserId = userId,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.RoutineTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/templates/{template.Id}", ToResponse(template, 0));
    }

    [HttpPost("{id:guid}/copy")]
    public async Task<IActionResult> Copy(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await TemplateCatalog.EnsureSeededAsync(db, cancellationToken);
        var template = await db.RoutineTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound(new ErrorResponse("Modelo nao encontrado."));
        }

        await db.RoutineTemplates
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UseCount, x => x.UseCount + 1), cancellationToken);

        if (template.Kind == RoutineTemplateKind.List)
        {
            var payload = JsonSerializer.Deserialize<ListPayload>(template.PayloadJson, Json)
                ?? new ListPayload();
            var list = new SharedList
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = template.Name,
                Kind = EnumText.ParseListKind(payload.Kind),
                MemberAccess = SharedListAccess.View,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            };
            db.SharedLists.Add(list);
            var order = 0;
            foreach (var item in payload.Items)
            {
                db.SharedListItems.Add(new SharedListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = list.Id,
                    Text = item.Text,
                    Quantity = item.Quantity,
                    SortOrder = order++,
                    AddedByUserId = userId,
                    CreatedAt = list.CreatedAt,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            return Created($"/api/lists/{list.Id}", new { kind = "list", id = list.Id, name = list.Name });
        }

        var reminderPayload = JsonSerializer.Deserialize<ReminderPayload>(template.PayloadJson, Json)
            ?? new ReminderPayload();
        var rule = reminderPayload.Recurrence ?? new RecurrenceRule();
        var start = clock.GetUtcNow().UtcDateTime;
        var next = RecurrenceCalculator.NextOnOrAfter(rule, start, start, 0) ?? start;
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(reminderPayload.Title) ? template.Name : reminderPayload.Title,
            Kind = EnumText.ParseReminderKind(reminderPayload.Kind),
            RecurrenceJson = rule.ToJson(),
            StartAt = start,
            NextFireAt = next,
            PersonName = reminderPayload.PersonName,
            GiftIdeasJson = "[]",
            CreatedAt = start,
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/reminders/{reminder.Id}", new { kind = "reminder", id = reminder.Id, title = reminder.Title });
    }

    private static TemplateResponse ToResponse(RoutineTemplate template, int rank) =>
        new(template.Id, EnumText.TemplateKind(template.Kind), template.Name, template.Description, template.UseCount, rank, template.Curated);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed class ListPayload
    {
        public string Kind { get; set; } = "other";
        public List<ListItemPayload> Items { get; set; } = [];
    }

    private sealed class ListItemPayload
    {
        public string Text { get; set; } = string.Empty;
        public string? Quantity { get; set; }
    }

    private sealed class ReminderPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = "general";
        public RecurrenceRule? Recurrence { get; set; }
        public string? PersonName { get; set; }
    }
}
