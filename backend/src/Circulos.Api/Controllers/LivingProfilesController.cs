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
[Route("api/living-profiles")]
public sealed class LivingProfilesController(AppDbContext db, IFileStorage storage, QuotaService quota) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LivingProfileResponse>>> List(
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

            var shared = await db.LivingProfiles.AsNoTracking()
                .Where(x => x.CircleId == cid)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
            return Ok(shared.Select(x => ToResponse(x, x.OwnerUserId == userId)).ToList());
        }

        var owned = await db.LivingProfiles.AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(owned.Select(x => ToResponse(x, true)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<LivingProfileResponse>> Create(
        [FromBody] LivingProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        Guid? circleId = null;
        if (request.CircleId is Guid cid)
        {
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            var member = circle?.Members.FirstOrDefault(x => x.UserId == userId);
            if (circle is null || CircleAccess.HiddenSurprise(circle, userId) || member is null)
            {
                return NotFound(new ErrorResponse("Circulo nao encontrado."));
            }

            if (member.Role < CircleRole.Editor)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Seu papel neste circulo nao liga um perfil."));
            }

            circleId = cid;
        }

        var now = DateTime.UtcNow;
        var profile = new LivingProfile
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            CircleId = circleId,
            Kind = EnumText.ParseLivingKind(request.Kind),
            Name = request.Name.Trim(),
            Context = request.Context?.Trim(),
            BirthDate = request.BirthDate,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.LivingProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/living-profiles/{profile.Id}", ToResponse(profile, true));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LivingProfileResponse>> Update(
        Guid id,
        [FromBody] LivingProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await db.LivingProfiles.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        if (request.CircleId is Guid cid)
        {
            var circle = await db.Circles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
            if (circle is null || circle.Members.All(x => x.UserId != userId))
            {
                return BadRequest(new ErrorResponse("Voce precisa participar do circulo."));
            }

            profile.CircleId = cid;
        }
        else
        {
            profile.CircleId = null;
        }

        profile.Kind = EnumText.ParseLivingKind(request.Kind);
        profile.Name = request.Name.Trim();
        profile.Context = request.Context?.Trim();
        profile.BirthDate = request.BirthDate;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(profile, true));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await db.LivingProfiles.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        if (profile.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(profile.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(userId, profile.PhotoSizeBytes, cancellationToken);
        }

        db.LivingProfiles.Remove(profile);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<LivingProfileResponse>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await db.LivingProfiles.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
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

        var key = $"{userId:N}/living/{profile.Id:N}.jpg";
        await storage.SaveAsync(key, compressed.Content, cancellationToken);
        if (profile.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(profile.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(userId, profile.PhotoSizeBytes, cancellationToken);
        }

        profile.PhotoStorageKey = key;
        profile.PhotoContentType = compressed.ContentType;
        profile.PhotoSizeBytes = compressed.SizeBytes;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(profile, true));
    }

    private static LivingProfileResponse ToResponse(LivingProfile profile, bool canEdit) =>
        new(
            profile.Id,
            EnumText.LivingKind(profile.Kind),
            profile.Name,
            profile.Context,
            profile.BirthDate,
            profile.CircleId,
            profile.PhotoStorageKey is not null,
            canEdit);
}
