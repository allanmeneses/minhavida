using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/life-areas")]
public sealed class LifeAreasController(AppDbContext db, IFileStorage storage, QuotaService quota, UsageStats stats) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LifeAreaResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var items = await db.LifeAreas
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new LifeAreaResponse(x.Id, x.Name, EnumText.Area(x.Kind), x.SortOrder, x.Suggested))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<LifeAreaResponse>> Create(
        [FromBody] CreateLifeAreaRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var max = await db.LifeAreas.Where(x => x.UserId == userId).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;
        var area = new LifeArea
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Kind = LifeAreaKind.Custom,
            SortOrder = max + 1,
            Suggested = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.LifeAreas.Add(area);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/life-areas/{area.Id}", new LifeAreaResponse(area.Id, area.Name, EnumText.Area(area.Kind), area.SortOrder, area.Suggested));
    }

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<IReadOnlyList<LifeEntryResponse>>> Entries(
        Guid id,
        [FromQuery] string? tag,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        var area = await OwnedArea(id, cancellationToken);
        if (area is null)
        {
            return NotFound(new ErrorResponse("Área não encontrada."));
        }

        var items = await db.LifeEntries
            .AsNoTracking()
            .Include(x => x.Shares)
            .Where(x => x.LifeAreaId == id)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var needle = tag.Trim();
            items = items
                .Where(x => TagList.FromJson(x.TagsJson).Any(item => string.Equals(item, needle, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var page = ListPaging.Clamp(skip, take);
        Response.Headers["X-Total-Count"] = items.Count.ToString();
        var slice = page.Take == int.MaxValue ? items.Skip(page.Skip) : items.Skip(page.Skip).Take(page.Take);
        return Ok(slice.Select(ToEntry).ToList());
    }

    [HttpPost("{id:guid}/entries")]
    public async Task<ActionResult<LifeEntryResponse>> CreateEntry(
        Guid id,
        [FromBody] LifeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var area = await OwnedArea(id, cancellationToken);
        if (area is null)
        {
            return NotFound(new ErrorResponse("Área não encontrada."));
        }

        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var now = DateTime.UtcNow;
        var entry = new LifeEntry
        {
            Id = Guid.NewGuid(),
            LifeAreaId = area.Id,
            UserId = area.UserId,
            Title = request.Title.Trim(),
            Body = request.Body?.Trim(),
            NumberValue = request.NumberValue,
            OccurredOn = request.OccurredOn,
            Location = request.Location?.Trim(),
            TagsJson = TagList.ToJson(request.Tags),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.LifeEntries.Add(entry);
        var owner = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        StreakService.Touch(owner, now);
        await db.SaveChangesAsync(cancellationToken);
        await stats.IncrementAsync(UsageStats.EntryCreated, cancellationToken);
        if (request.CircleIds is { Length: > 0 })
        {
            var shareError = await ReplaceSharesAsync(entry, request.CircleIds, userId, now, cancellationToken);
            if (shareError is not null)
            {
                return BadRequest(new ErrorResponse(shareError));
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await db.Entry(entry).Collection(x => x.Shares).LoadAsync(cancellationToken);
        return Created($"/api/life-areas/{id}/entries/{entry.Id}", ToEntry(entry));
    }

    [HttpPut("{id:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<LifeEntryResponse>> UpdateEntry(
        Guid id,
        Guid entryId,
        [FromBody] LifeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await OwnedEntry(id, entryId, cancellationToken);
        if (entry is null)
        {
            return NotFound(new ErrorResponse("Entrada não encontrada."));
        }

        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        entry.Title = request.Title.Trim();
        entry.Body = request.Body?.Trim();
        entry.NumberValue = request.NumberValue;
        entry.OccurredOn = request.OccurredOn;
        entry.Location = request.Location?.Trim();
        entry.TagsJson = TagList.ToJson(request.Tags);
        entry.UpdatedAt = DateTime.UtcNow;
        if (request.CircleIds is not null)
        {
            var shareError = await ReplaceSharesAsync(entry, request.CircleIds, userId, entry.UpdatedAt, cancellationToken);
            if (shareError is not null)
            {
                return BadRequest(new ErrorResponse(shareError));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(entry).Collection(x => x.Shares).LoadAsync(cancellationToken);
        return Ok(ToEntry(entry));
    }

    [HttpPost("{id:guid}/entries/{entryId:guid}/share")]
    public async Task<ActionResult<LifeEntryResponse>> ShareEntry(
        Guid id,
        Guid entryId,
        [FromBody] ShareEntryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var entry = await OwnedEntry(id, entryId, cancellationToken);
        if (entry is null)
        {
            return NotFound(new ErrorResponse("Entrada não encontrada."));
        }

        var shareError = await ReplaceSharesAsync(entry, request.CircleIds ?? [], userId, DateTime.UtcNow, cancellationToken);
        if (shareError is not null)
        {
            return BadRequest(new ErrorResponse(shareError));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(entry).Collection(x => x.Shares).LoadAsync(cancellationToken);
        return Ok(ToEntry(entry));
    }

    [HttpDelete("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid id, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await OwnedEntry(id, entryId, cancellationToken);
        if (entry is null)
        {
            return NotFound(new ErrorResponse("Entrada não encontrada."));
        }

        if (entry.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(entry.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(entry.UserId, entry.PhotoSizeBytes, cancellationToken);
        }

        db.LifeEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/entries/{entryId:guid}/photo")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<LifeEntryResponse>> UploadPhoto(
        Guid id,
        Guid entryId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var entry = await OwnedEntry(id, entryId, cancellationToken);
        if (entry is null)
        {
            return NotFound(new ErrorResponse("Entrada não encontrada."));
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
        var quotaError = await quota.TryAddStorageAsync(entry.UserId, compressed.SizeBytes, cancellationToken);
        if (quotaError is not null)
        {
            return BadRequest(new ErrorResponse(quotaError));
        }

        var key = $"{entry.UserId:N}/entries/{entry.Id:N}.jpg";
        await storage.SaveAsync(key, compressed.Content, cancellationToken);
        if (entry.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(entry.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(entry.UserId, entry.PhotoSizeBytes, cancellationToken);
        }

        entry.PhotoStorageKey = key;
        entry.PhotoContentType = compressed.ContentType;
        entry.PhotoSizeBytes = compressed.SizeBytes;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToEntry(entry));
    }

    [HttpGet("{id:guid}/entries/{entryId:guid}/photo")]
    public async Task<IActionResult> DownloadPhoto(Guid id, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await OwnedEntry(id, entryId, cancellationToken);
        if (entry is null || entry.PhotoStorageKey is null || entry.PhotoContentType is null)
        {
            return NotFound(new ErrorResponse("Esta entrada ainda não tem foto."));
        }

        var stream = await storage.OpenReadAsync(entry.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, entry.PhotoContentType, "entrada.jpg");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> ExportDiary(Guid id, CancellationToken cancellationToken)
    {
        var area = await OwnedArea(id, cancellationToken);
        if (area is null)
        {
            return NotFound(new ErrorResponse("Área não encontrada."));
        }

        var entries = await db.LifeEntries
            .AsNoTracking()
            .Where(x => x.LifeAreaId == id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var pages = await db.DiaryPages.AsNoTracking()
            .Where(x => x.LifeAreaId == id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var text = new StringBuilder();
        text.AppendLine(area.Name);
        foreach (var page in pages)
        {
            text.AppendLine();
            text.AppendLine(page.Title);
            if (!string.IsNullOrWhiteSpace(page.Body))
            {
                text.AppendLine(page.Body);
            }
        }

        foreach (var entry in entries)
        {
            text.AppendLine();
            text.AppendLine(entry.Title);
            if (entry.OccurredOn is { } day)
            {
                text.AppendLine(day.ToString("yyyy-MM-dd"));
            }

            if (!string.IsNullOrWhiteSpace(entry.Location))
            {
                text.AppendLine(entry.Location);
            }

            if (!string.IsNullOrWhiteSpace(entry.Body))
            {
                text.AppendLine(entry.Body);
            }
        }

        return File(Encoding.UTF8.GetBytes(text.ToString()), "text/plain", $"{area.Name}.txt");
    }

    [HttpGet("{id:guid}/pages")]
    public async Task<ActionResult<IReadOnlyList<DiaryPageResponse>>> Pages(Guid id, CancellationToken cancellationToken)
    {
        var area = await OwnedArea(id, cancellationToken);
        if (area is null || area.Kind != LifeAreaKind.Diary)
        {
            return NotFound(new ErrorResponse("Diário não encontrado."));
        }

        var pages = await db.DiaryPages.AsNoTracking()
            .Where(x => x.LifeAreaId == id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new DiaryPageResponse(x.Id, x.Title, x.Body, x.SortOrder, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(pages);
    }

    [HttpGet("{id:guid}/search")]
    public async Task<ActionResult<IReadOnlyList<SearchHit>>> SearchDiary(
        Guid id,
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        var area = await OwnedArea(id, cancellationToken);
        if (area is null || area.Kind != LifeAreaKind.Diary)
        {
            return NotFound(new ErrorResponse("Diário não encontrado."));
        }

        var term = (q ?? string.Empty).Trim();
        if (term.Length < 1)
        {
            return Ok(Array.Empty<SearchHit>());
        }

        var lowered = term.ToLowerInvariant();
        var hits = new List<SearchHit>();
        var pages = await db.DiaryPages.AsNoTracking()
            .Where(x => x.LifeAreaId == id
                && (x.Title.ToLower().Contains(lowered) || x.Body.ToLower().Contains(lowered)))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);
        hits.AddRange(pages.Select(page => new SearchHit("diary", page.Id, page.Title, page.Body)));
        var entries = await db.LifeEntries.AsNoTracking()
            .Where(x => x.LifeAreaId == id
                && (x.Title.ToLower().Contains(lowered)
                    || (x.Body != null && x.Body.ToLower().Contains(lowered))
                    || x.TagsJson.ToLower().Contains(lowered)))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);
        hits.AddRange(entries.Select(entry => new SearchHit("diary", entry.Id, entry.Title, entry.Body)));
        return Ok(hits);
    }

    [HttpPost("{id:guid}/pages")]
    public async Task<ActionResult<DiaryPageResponse>> CreatePage(
        Guid id,
        [FromBody] DiaryPageRequest request,
        CancellationToken cancellationToken)
    {
        var area = await OwnedArea(id, cancellationToken);
        if (area is null || area.Kind != LifeAreaKind.Diary)
        {
            return NotFound(new ErrorResponse("Diário não encontrado."));
        }

        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var max = await db.DiaryPages.Where(x => x.LifeAreaId == id).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0;
        var now = DateTime.UtcNow;
        var page = new DiaryPage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LifeAreaId = id,
            Title = request.Title.Trim(),
            Body = request.Body?.Trim() ?? string.Empty,
            SortOrder = max + 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.DiaryPages.Add(page);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Created($"/api/life-areas/{id}/pages/{page.Id}", new DiaryPageResponse(page.Id, page.Title, page.Body, page.SortOrder, page.UpdatedAt));
    }

    private Task<LifeArea?> OwnedArea(Guid id, CancellationToken cancellationToken) =>
        db.LifeAreas.FirstOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);

    private Task<LifeEntry?> OwnedEntry(Guid areaId, Guid entryId, CancellationToken cancellationToken) =>
        db.LifeEntries.FirstOrDefaultAsync(
            x => x.Id == entryId && x.LifeAreaId == areaId && x.UserId == User.GetUserId(),
            cancellationToken);

    private async Task<string?> ReplaceSharesAsync(
        LifeEntry entry,
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
                .Where(x => x.UserId == userId && wanted.Contains(x.CircleId) && x.Circle.Status == CircleStatus.Active)
                .Select(x => x.CircleId)
                .ToListAsync(cancellationToken);
            if (allowed.Count != wanted.Length)
            {
                return "Você precisa participar do círculo para mostrar a entrada lá.";
            }
        }

        var current = await db.LifeEntryShares
            .Where(x => x.LifeEntryId == entry.Id)
            .ToListAsync(cancellationToken);
        foreach (var share in current.Where(x => !wanted.Contains(x.CircleId)))
        {
            db.LifeEntryShares.Remove(share);
        }

        var already = current.Select(x => x.CircleId).ToHashSet();
        foreach (var circleId in wanted.Where(id => !already.Contains(id)))
        {
            db.LifeEntryShares.Add(new LifeEntryShare
            {
                Id = Guid.NewGuid(),
                LifeEntryId = entry.Id,
                CircleId = circleId,
                CreatedAt = now,
            });
        }

        return null;
    }

    private static LifeEntryResponse ToEntry(LifeEntry entry) =>
        new(
            entry.Id,
            entry.LifeAreaId,
            entry.Title,
            entry.Body,
            entry.NumberValue,
            entry.OccurredOn,
            entry.Location,
            entry.PhotoStorageKey is not null,
            entry.CreatedAt,
            entry.UpdatedAt,
            TagList.FromJson(entry.TagsJson),
            entry.Shares.Select(x => x.CircleId).OrderBy(x => x).ToList());
}
