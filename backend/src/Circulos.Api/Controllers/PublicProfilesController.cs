using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/profiles")]
public sealed class PublicProfilesController(AppDbContext db, IFileStorage storage) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<PublicProfileResponse>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is null || !profile.IsPublic)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        Response.Headers.CacheControl = "private, no-store";
        return Ok(new PublicProfileResponse(
            profile.UserId,
            profile.DisplayName,
            profile.PhotoStorageKey is not null,
            profile.Bio));
    }

    [HttpGet("{userId:guid}/photo")]
    public async Task<IActionResult> Photo(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is null || !profile.IsPublic || profile.PhotoStorageKey is null || profile.PhotoContentType is null)
        {
            return NotFound(new ErrorResponse("Perfil nao encontrado."));
        }

        var stream = await storage.OpenReadAsync(profile.PhotoStorageKey, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, profile.PhotoContentType, profile.PhotoFileName ?? "foto.jpg");
    }
}
