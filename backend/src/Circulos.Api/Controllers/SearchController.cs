using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
public sealed class SearchController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string q,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var term = (q ?? string.Empty).Trim();
        if (term.Length < 1)
        {
            return Ok(new SearchResponse([], await History(userId, cancellationToken)));
        }

        await RecordAsync(userId, term, cancellationToken);
        var lowered = term.ToLowerInvariant();
        var hits = new List<SearchHit>();
        var filter = type?.Trim().ToLowerInvariant();

        if (filter is null or "" or "entry" or "diary")
        {
            var entries = await db.LifeEntries.AsNoTracking()
                .Include(x => x.LifeArea)
                .Where(x => x.UserId == userId)
                .Where(x => x.Title.ToLower().Contains(lowered) || (x.Body != null && x.Body.ToLower().Contains(lowered)))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(40)
                .ToListAsync(cancellationToken);
            foreach (var entry in entries)
            {
                var kind = entry.LifeArea.Kind == LifeAreaKind.Diary ? "diary" : "entry";
                if (filter is { Length: > 0 } && filter != kind)
                {
                    continue;
                }

                hits.Add(new SearchHit(kind, entry.Id, entry.Title, entry.Body));
            }
        }

        if (filter is null or "" or "circle")
        {
            var memberIds = await db.CircleMembers.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.CircleId)
                .ToListAsync(cancellationToken);
            var circles = await db.Circles.AsNoTracking()
                .Where(x => memberIds.Contains(x.Id) || (x.Discoverable && x.Status == CircleStatus.Active))
                .Where(x => x.Name.ToLower().Contains(lowered) || x.Description.ToLower().Contains(lowered))
                .Take(20)
                .ToListAsync(cancellationToken);
            hits.AddRange(circles.Select(circle => new SearchHit("circle", circle.Id, circle.Name, circle.Description)));
        }

        if (filter is null or "" or "birthday")
        {
            var birthdays = await db.Reminders.AsNoTracking()
                .Where(x => x.UserId == userId && x.Kind == ReminderKind.Birthday)
                .Where(x => x.Title.ToLower().Contains(lowered) || (x.PersonName != null && x.PersonName.ToLower().Contains(lowered)))
                .Take(20)
                .ToListAsync(cancellationToken);
            hits.AddRange(birthdays.Select(item => new SearchHit("birthday", item.Id, item.Title, item.PersonName)));
        }

        return Ok(new SearchResponse(hits, await History(userId, cancellationToken)));
    }

    [HttpGet("history")]
    public async Task<ActionResult<SearchHistoryResponse>> HistoryList(CancellationToken cancellationToken) =>
        Ok(await History(User.GetUserId(), cancellationToken));

    private async Task RecordAsync(Guid userId, string term, CancellationToken cancellationToken)
    {
        var normalized = term.Trim().ToLowerInvariant();
        if (normalized.Length > 120)
        {
            normalized = normalized[..120];
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await db.SearchQueries.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Term == normalized,
            cancellationToken);
        if (existing is null)
        {
            db.SearchQueries.Add(new SearchQuery
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Term = normalized,
                TimesUsed = 1,
                LastUsedAt = now,
            });
        }
        else
        {
            existing.TimesUsed += 1;
            existing.LastUsedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SearchHistoryResponse> History(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.SearchQueries.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var recent = rows.OrderByDescending(x => x.LastUsedAt).Select(x => x.Term).Take(10).ToList();
        var frequent = rows.Where(x => x.TimesUsed > 0).OrderByDescending(x => x.TimesUsed).ThenByDescending(x => x.LastUsedAt).Select(x => x.Term).Take(10).ToList();
        return new SearchHistoryResponse(recent, frequent);
    }
}
