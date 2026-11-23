using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(AppDbContext db, IFileStorage storage, QuotaService quota) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil não encontrado."));
        }

        return Ok(ToResponse(profile));
    }

    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> Update(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BirthDate is { } birthDate)
        {
            if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return BadRequest(new ErrorResponse("A data de nascimento não pode ser no futuro."));
            }

            if (birthDate < new DateOnly(1900, 1, 1))
            {
                return BadRequest(new ErrorResponse("Informe uma data de nascimento válida."));
            }
        }

        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil não encontrado."));
        }

        profile.DisplayName = request.DisplayName.Trim();
        profile.BirthDate = request.BirthDate;
        profile.Phone = EmptyToNull(request.Phone);
        profile.SecondaryEmail = EmptyToNull(request.SecondaryEmail)?.ToLowerInvariant();
        profile.IsPublic = request.IsPublic;
        profile.Bio = EmptyToNull(request.Bio);
        profile.FindableByLocation = request.FindableByLocation;
        LocationPrivacy.ClearUserCoordinates(profile);
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(profile));
    }

    [HttpPost("photo")]
    [RequestSizeLimit(FileRules.MaxIncomingBytes + 1024)]
    public async Task<ActionResult<ProfileResponse>> UploadPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
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

        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil não encontrado."));
        }

        await using var original = file.OpenReadStream();
        await using var compressed = await ImageCompressor.CompressAsync(original, cancellationToken);
        var quotaError = await quota.TryAddStorageAsync(User.GetUserId(), compressed.SizeBytes, cancellationToken);
        if (quotaError is not null)
        {
            return BadRequest(new ErrorResponse(quotaError));
        }

        var userId = User.GetUserId();
        var key = $"{userId:N}/photo/{Guid.NewGuid():N}.jpg";
        await storage.SaveAsync(key, compressed.Content, cancellationToken);

        if (profile.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(profile.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(userId, profile.PhotoSizeBytes, cancellationToken);
        }

        profile.PhotoStorageKey = key;
        profile.PhotoContentType = compressed.ContentType;
        profile.PhotoFileName = compressed.FileName;
        profile.PhotoSizeBytes = compressed.SizeBytes;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(profile));
    }

    [HttpGet("photo")]
    public async Task<IActionResult> DownloadPhoto(CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null || profile.PhotoStorageKey is null || profile.PhotoContentType is null)
        {
            return NotFound(new ErrorResponse("Este perfil ainda não tem foto."));
        }

        var stream = await storage.OpenReadAsync(profile.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, profile.PhotoContentType, profile.PhotoFileName ?? "foto.jpg");
    }

    [HttpDelete("photo")]
    public async Task<ActionResult<ProfileResponse>> DeletePhoto(CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil não encontrado."));
        }

        if (profile.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(profile.PhotoStorageKey, cancellationToken);
            await quota.ReleaseStorageAsync(User.GetUserId(), profile.PhotoSizeBytes, cancellationToken);
        }

        profile.PhotoStorageKey = null;
        profile.PhotoContentType = null;
        profile.PhotoFileName = null;
        profile.PhotoSizeBytes = 0;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(profile));
    }

    private static ProfileResponse ToResponse(Domain.Profile profile) =>
        new(
            profile.DisplayName,
            profile.BirthDate,
            profile.Phone,
            profile.SecondaryEmail,
            profile.IsPublic,
            profile.PhotoStorageKey is not null,
            profile.Bio,
            profile.FindableByLocation);

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
