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
public sealed class RecapController(AppDbContext db, RecapService recap, CircleArchiveService archive) : ControllerBase
{
    [HttpGet("api/recap/me")]
    public async Task<ActionResult<ProfileRecapResponse>> Mine(CancellationToken cancellationToken)
    {
        return Ok(await recap.ForProfileAsync(User.GetUserId(), cancellationToken));
    }

    [HttpPost("api/recap/monthly/ack")]
    public async Task<IActionResult> AckMonthly(CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstAsync(item => item.Id == User.GetUserId(), cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var previous = today.AddDays(1 - today.Day).AddDays(-1);
        user.LastMonthlyRecapSeenOn = new DateOnly(previous.Year, previous.Month, 1);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("api/circles/{id:guid}/recap")]
    public async Task<ActionResult<CircleRecapResponse>> Circle(Guid id, CancellationToken cancellationToken)
    {
        await archive.ArchiveIfDueAsync(id, cancellationToken);
        var result = await recap.ForCircleAsync(User.GetUserId(), id, cancellationToken);
        if (result is null)
        {
            return NotFound(new ErrorResponse("Círculo não encontrado."));
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
            return NotFound(new ErrorResponse("Círculo não encontrado."));
        }

        var user = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        if (user.Plan == PlanKind.Paid && !PlanLimits.BenefitsActive(user, DateTime.UtcNow))
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new ErrorResponse("Assinatura suspensa. Compartilhar retrospectiva fica bloqueado até voltar a pagar."));
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
            return NotFound(new ErrorResponse("Recap não encontrado."));
        }

        Response.Headers.CacheControl = "private, no-store";
        return Ok(recap.Deserialize(share.PayloadJson));
    }
}
