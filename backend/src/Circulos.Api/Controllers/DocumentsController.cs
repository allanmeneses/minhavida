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
[Route("api/documents")]
public sealed class DocumentsController(AppDbContext db, IFileStorage storage, QuotaService quota) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var items = await db.Documents
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DocumentResponse(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<DocumentResponse>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse("Envie um arquivo."));
        }

        var fileName = FileRules.SafeFileName(file.FileName);
        var error = FileRules.ValidateDocument(fileName, file.ContentType, file.Length);
        if (error is not null)
        {
            return BadRequest(new ErrorResponse(error));
        }

        var userId = User.GetUserId();
        var documentId = Guid.NewGuid();
        var key = $"{userId:N}/docs/{documentId:N}";
        var stored = false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await UserRowLock.AcquireAsync(db, userId, cancellationToken);
            var quotaError = await quota.TryAddStorageAsync(userId, file.Length, cancellationToken);
            if (quotaError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(new ErrorResponse(quotaError));
            }

            await using (var stream = file.OpenReadStream())
            {
                await storage.SaveAsync(key, stream, cancellationToken);
            }

            stored = true;
            var document = new PersonalDocument
            {
                Id = documentId,
                UserId = userId,
                FileName = fileName,
                ContentType = FileRules.CanonicalContentType(fileName, file.ContentType),
                SizeBytes = file.Length,
                StorageKey = key,
                CreatedAt = DateTime.UtcNow,
            };

            db.Documents.Add(document);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Created($"/api/documents/{document.Id}/download", ToResponse(document));
        }
        catch (Exception ex) when (UserRowLock.IsMissingUser(ex))
        {
            if (stored)
            {
                await storage.DeleteAsync(key, cancellationToken);
            }

            return BadRequest(new ErrorResponse("Conta nao encontrada."));
        }
        catch
        {
            if (stored)
            {
                await storage.DeleteAsync(key, cancellationToken);
            }

            throw;
        }
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await FindOwnedAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound(new ErrorResponse("Documento nao encontrado."));
        }

        var stream = await storage.OpenReadAsync(document.StorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, document.ContentType, document.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var document = await FindOwnedAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound(new ErrorResponse("Documento nao encontrado."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        await quota.ReleaseStorageAsync(document.UserId, document.SizeBytes, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await storage.DeleteAsync(document.StorageKey, cancellationToken);
        return NoContent();
    }

    private Task<PersonalDocument?> FindOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return db.Documents.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    private static DocumentResponse ToResponse(PersonalDocument document) =>
        new(document.Id, document.FileName, document.ContentType, document.SizeBytes, document.CreatedAt);
}
