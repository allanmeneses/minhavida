using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(AppDbContext db, IFileStorage storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
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
                return BadRequest(new ErrorResponse("A data de nascimento nao pode ser no futuro."));
            }

            if (birthDate < new DateOnly(1900, 1, 1))
            {
                return BadRequest(new ErrorResponse("Informe uma data de nascimento valida."));
            }
        }

        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        profile.DisplayName = request.DisplayName.Trim();
        profile.BirthDate = request.BirthDate;
        profile.Phone = EmptyToNull(request.Phone);
        profile.SecondaryEmail = EmptyToNull(request.SecondaryEmail)?.ToLowerInvariant();
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(profile));
    }

    [HttpPost("photo")]
    [RequestSizeLimit(FileRules.MaxPhotoBytes + 1024)]
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
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        var userId = User.GetUserId();
        var key = $"{userId:N}/photo/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await using (var stream = file.OpenReadStream())
        {
            await storage.SaveAsync(key, stream, cancellationToken);
        }

        if (profile.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(profile.PhotoStorageKey, cancellationToken);
        }

        profile.PhotoStorageKey = key;
        profile.PhotoContentType = FileRules.CanonicalContentType(fileName, file.ContentType);
        profile.PhotoFileName = fileName;
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
            return NotFound(new ErrorResponse("Este perfil ainda nao tem foto."));
        }

        var stream = await storage.OpenReadAsync(profile.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, profile.PhotoContentType, profile.PhotoFileName ?? "foto");
    }

    [HttpDelete("photo")]
    public async Task<ActionResult<ProfileResponse>> DeletePhoto(CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        if (profile.PhotoStorageKey is not null)
        {
            await storage.DeleteAsync(profile.PhotoStorageKey, cancellationToken);
        }

        profile.PhotoStorageKey = null;
        profile.PhotoContentType = null;
        profile.PhotoFileName = null;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(profile));
    }

    private static ProfileResponse ToResponse(Domain.Profile profile) =>
        new(profile.DisplayName, profile.BirthDate, profile.Phone, profile.SecondaryEmail, profile.PhotoStorageKey is not null);

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
