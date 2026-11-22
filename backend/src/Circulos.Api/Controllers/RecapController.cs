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
public sealed class RecapController(AppDbContext db, RecapService recap) : ControllerBase
{
    [HttpGet("api/recap/me")]
    public async Task<ActionResult<ProfileRecapResponse>> Mine(CancellationToken cancellationToken)
    {
        return Ok(await recap.ForProfileAsync(User.GetUserId(), cancellationToken));
    }

    [HttpGet("api/circles/{id:guid}/recap")]
    public async Task<ActionResult<CircleRecapResponse>> Circle(Guid id, CancellationToken cancellationToken)
    {
        var result = await recap.ForCircleAsync(User.GetUserId(), id, cancellationToken);
        if (result is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        return Ok(result);
    }

    [HttpPost("api/circles/{id:guid}/recap/share")]
    public async Task<ActionResult<RecapShareCreatedResponse>> Share(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var snapshot = await recap.ForCircleAsync(userId, id, cancellationToken);
        if (snapshot is null)
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var share = new RecapShare
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CircleId = id,
            Token = TokenHash.NewToken().ToLowerInvariant(),
            PayloadJson = recap.Serialize(snapshot),
            CreatedAt = DateTime.UtcNow,
        };
        db.RecapShares.Add(share);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new RecapShareCreatedResponse(share.Token, $"/r/{share.Token}"));
    }

    [AllowAnonymous]
    [HttpGet("api/public/recaps/{token}")]
    public async Task<ActionResult<CircleRecapResponse>> Public(string token, CancellationToken cancellationToken)
    {
        var share = await db.RecapShares.AsNoTracking().FirstOrDefaultAsync(item => item.Token == token, cancellationToken);
        if (share is null)
        {
            return NotFound(new ErrorResponse("Recap nao encontrado."));
        }

        Response.Headers.CacheControl = "private, no-store";
        return Ok(recap.Deserialize(share.PayloadJson));
    }
}
