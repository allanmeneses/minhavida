using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/memories")]
public sealed class MemoriesController(AppDbContext db, IFileStorage storage, QuotaService quota) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemoryResponse>>> List(
        [FromQuery] Guid? circleId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (circleId is Guid cid)
        {
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            if (circle is null || CircleAccess.HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
            {
                return NotFound(new ErrorResponse("Circulo nao encontrado."));
            }

            var circleItems = await db.Memories.AsNoTracking()
                .Where(x => x.CircleId == cid)
                .OrderByDescending(x => x.OccurredOn)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
            return Ok(circleItems.Select(ToResponse).ToList());
        }

        var solo = await db.Memories.AsNoTracking()
            .Where(x => x.AuthorUserId == userId && x.CircleId == null)
            .OrderByDescending(x => x.OccurredOn)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(solo.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MemoryResponse>> Create(
        [FromBody] MemoryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        Circle? circle = null;
        if (request.CircleId is Guid cid)
        {
            circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
            if (circle is null || CircleAccess.HiddenSurprise(circle, userId) || member is null)
            {
                return NotFound(new ErrorResponse("Circulo nao encontrado."));
            }

            if (circle.Status != CircleStatus.Active)
            {
                return BadRequest(new ErrorResponse("Este circulo esta travado."));
            }

            if (member.Role < CircleRole.Editor)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Seu papel neste circulo nao cria memorias."));
            }
        }

        var now = DateTime.UtcNow;
        var memory = new Memory
        {
            Id = Guid.NewGuid(),
            AuthorUserId = userId,
            CircleId = circle?.Id,
            Title = request.Title.Trim(),
            Body = request.Body?.Trim() ?? string.Empty,
            OccurredOn = request.OccurredOn,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Memories.Add(memory);
        if (circle is not null)
        {
            db.CircleLogs.Add(new CircleLog
            {
                Id = Guid.NewGuid(),
                CircleId = circle.Id,
                ActorUserId = userId,
                Action = "memory",
                Detail = "Memoria adicionada.",
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/memories/{memory.Id}", ToResponse(memory));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MemoryResponse>> Update(
        Guid id,
        [FromBody] MemoryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var memory = await db.Memories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (memory is null)
        {
            return NotFound(new ErrorResponse("Memoria nao encontrada."));
        }

        if (memory.AuthorUserId != userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("So quem guardou edita esta memoria."));
        }

        memory.Title = request.Title.Trim();
        memory.Body = request.Body?.Trim() ?? string.Empty;
        memory.OccurredOn = request.OccurredOn;
        memory.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(memory));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var memory = await db.Memories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (memory is null)
        {
            return NotFound(new ErrorResponse("Memoria nao encontrada."));
        }

        if (memory.CircleId is Guid cid)
        {
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
            if (circle is null || member is null)
            {
                return NotFound(new ErrorResponse("Memoria nao encontrada."));
            }

            if (memory.AuthorUserId != userId && member.Role < CircleRole.Admin)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Sem permissao para apagar esta memoria."));
            }
        }
        else if (memory.AuthorUserId != userId)
        {
            return NotFound(new ErrorResponse("Memoria nao encontrada."));
        }

        if (memory.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(memory.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(memory.AuthorUserId, memory.PhotoSizeBytes, cancellationToken);
        }

        db.Memories.Remove(memory);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<MemoryResponse>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var memory = await db.Memories.FirstOrDefaultAsync(x => x.Id == id && x.AuthorUserId == userId, cancellationToken);
        if (memory is null)
        {
            return NotFound(new ErrorResponse("Memoria nao encontrada."));
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
        var quotaError = await quota.TryAddStorageAsync(userId, compressed.SizeBytes, cancellationToken);
        if (quotaError is not null)
        {
            return BadRequest(new ErrorResponse(quotaError));
        }

        var key = $"{userId:N}/memories/{memory.Id:N}.jpg";
        await storage.SaveAsync(key, compressed.Content, cancellationToken);
        if (memory.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(memory.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(userId, memory.PhotoSizeBytes, cancellationToken);
        }

        memory.PhotoStorageKey = key;
        memory.PhotoContentType = compressed.ContentType;
        memory.PhotoSizeBytes = compressed.SizeBytes;
        memory.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(memory));
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var memory = await db.Memories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (memory is null || memory.PhotoStorageKey is null || memory.PhotoContentType is null)
        {
            return NotFound(new ErrorResponse("Esta memoria ainda nao tem foto."));
        }

        if (memory.CircleId is Guid cid)
        {
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            if (circle is null || CircleAccess.HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
            {
                return NotFound(new ErrorResponse("Memoria nao encontrada."));
            }
        }
        else if (memory.AuthorUserId != userId)
        {
            return NotFound(new ErrorResponse("Memoria nao encontrada."));
        }

        var stream = await storage.OpenReadAsync(memory.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, memory.PhotoContentType, "memoria.jpg");
    }

    private static MemoryResponse ToResponse(Memory memory) =>
        new(
            memory.Id,
            memory.AuthorUserId,
            memory.CircleId,
            memory.SourceCircleId,
            memory.Title,
            memory.Body,
            memory.OccurredOn,
            memory.PhotoStorageKey is not null,
            memory.CreatedAt,
            memory.UpdatedAt);
}
