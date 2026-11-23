using Circulos.Api.Auth;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdminController(
    AppDbContext db,
    UsageStats stats,
    SessionService sessions,
    IEmailSender email,
    IOptions<SystemAdminOptions> adminOptions) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsResponse>> Stats(CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var snapshot = await stats.SnapshotAsync(cancellationToken);
        return Ok(new AdminStatsResponse(snapshot.Shortcuts, snapshot.Entries, snapshot.ActiveCircles));
    }

    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<ReportItemResponse>>> Reports(CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var items = await db.ContentReports
            .AsNoTracking()
            .Include(item => item.Reporter)
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(item => new ReportItemResponse(
            item.Id,
            item.ReporterUserId,
            item.Reporter.Email,
            item.TargetKind,
            item.TargetId,
            item.Note,
            item.CreatedAt)).ToList());
    }

    [HttpGet("blocks")]
    public async Task<ActionResult<AdminBlockListResponse>> Blocks(CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var users = await db.Users.AsNoTracking()
            .Where(user => user.BlockedAt != null)
            .OrderByDescending(user => user.BlockedAt)
            .Select(user => new AdminBlockedUserResponse(user.Id, user.Email, user.BlockedAt!.Value))
            .ToListAsync(cancellationToken);
        var circles = await db.Circles.AsNoTracking()
            .Where(circle => circle.Status == CircleStatus.Blocked)
            .OrderBy(circle => circle.Name)
            .Select(circle => new AdminBlockedCircleResponse(circle.Id, circle.Name, null))
            .ToListAsync(cancellationToken);
        return Ok(new AdminBlockListResponse(users, circles));
    }

    [HttpGet("actions")]
    public async Task<ActionResult<IReadOnlyList<AdminActionResponse>>> Actions(CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var items = await db.AdminActions.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(item => new AdminActionResponse(
            item.Id,
            item.AdminUserId,
            item.Action,
            item.TargetKind,
            item.TargetId,
            item.Detail,
            item.CreatedAt)).ToList());
    }

    [HttpGet("appeals")]
    public async Task<ActionResult<IReadOnlyList<AppealResponse>>> Appeals(CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var items = await db.AccountAppeals.AsNoTracking()
            .Include(item => item.User)
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(item => new AppealResponse(
            item.Id,
            item.UserId,
            item.User.Email,
            item.Note,
            item.Status,
            item.CreatedAt,
            item.ResolvedAt)).ToList());
    }

    [HttpPost("appeals/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveAppeal(
        Guid id,
        [FromBody] ResolveAppealRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var appeal = await db.AccountAppeals.Include(item => item.User).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (appeal is null)
        {
            return NotFound(new ErrorResponse("Recurso não encontrado."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, appeal.UserId, cancellationToken);
        await db.Entry(appeal).ReloadAsync(cancellationToken);
        await db.Entry(appeal.User).ReloadAsync(cancellationToken);
        if (appeal.Status != "open")
        {
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }

        appeal.Status = request.Accept ? "accepted" : "rejected";
        appeal.ResolvedAt = DateTime.UtcNow;
        if (request.Accept)
        {
            appeal.User.BlockedAt = null;
        }

        db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = admin.Id,
            Action = request.Accept ? "appeal-accept" : "appeal-reject",
            TargetKind = "profile",
            TargetId = appeal.UserId,
            Detail = request.Accept ? "Recurso aceito. Sem prazo." : "Recurso recusado. Sem prazo.",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("blocks")]
    public async Task<IActionResult> SetBlock(
        [FromBody] AdminBlockRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await RequireAdmin(cancellationToken);
        if (admin is null)
        {
            return NotFound(new ErrorResponse("Painel não encontrado."));
        }

        var kind = ReportsController.NormalizeKind(request.TargetKind);
        if (kind is null || kind == "post")
        {
            return BadRequest(new ErrorResponse("Bloqueio vale para perfil ou círculo."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (kind == "profile")
        {
            await UserRowLock.AcquireAsync(db, request.TargetId, cancellationToken);
            var user = await db.Users.Include(item => item.Profile).FirstOrDefaultAsync(item => item.Id == request.TargetId, cancellationToken);
            if (user is null)
            {
                return NotFound(new ErrorResponse("Pessoa não encontrada."));
            }

            if (user.IsSystemAdmin || adminOptions.Value.Matches(user.Email))
            {
                return BadRequest(new ErrorResponse("Não é possível bloquear o admin do sistema."));
            }

            if (request.Blocked)
            {
                user.BlockedAt ??= DateTime.UtcNow;
                await sessions.RevokeAllAsync(user.Id, cancellationToken);
                await email.SendAsync(
                    user.Email,
                    "Conta bloqueada no Círculos",
                    "A equipe do Círculos bloqueou esta conta. Você pode recorrer por e-mail de suporte.",
                    cancellationToken);
            }
            else
            {
                user.BlockedAt = null;
            }
        }
        else
        {
            await CircleRowLock.AcquireAsync(db, request.TargetId, cancellationToken);
            var circle = await db.Circles.FirstOrDefaultAsync(item => item.Id == request.TargetId, cancellationToken);
            if (circle is null || circle.Status == CircleStatus.Deleted)
            {
                return NotFound(new ErrorResponse("Círculo não encontrado."));
            }

            if (request.Blocked)
            {
                if (circle.Status == CircleStatus.Archived)
                {
                    return BadRequest(new ErrorResponse("Círculo arquivado não entra no bloqueio de radar."));
                }

                circle.Status = CircleStatus.Blocked;
                db.CircleLogs.Add(new CircleLog
                {
                    Id = Guid.NewGuid(),
                    CircleId = circle.Id,
                    ActorUserId = admin.Id,
                    Action = "blocked",
                    Detail = "Círculo bloqueado pela equipe do app.",
                    CreatedAt = DateTime.UtcNow,
                });
                if (circle.OwnerUserId is Guid ownerId)
                {
                    var owner = await db.Users.FirstOrDefaultAsync(item => item.Id == ownerId, cancellationToken);
                    if (owner is not null)
                    {
                        await email.SendAsync(
                            owner.Email,
                            "Círculo bloqueado no Círculos",
                            $"O círculo {circle.Name} foi bloqueado pela equipe. Você pode recorrer por e-mail de suporte.",
                            cancellationToken);
                    }
                }
            }
            else if (circle.Status == CircleStatus.Blocked)
            {
                circle.Status = CircleStatus.Active;
                db.CircleLogs.Add(new CircleLog
                {
                    Id = Guid.NewGuid(),
                    CircleId = circle.Id,
                    ActorUserId = admin.Id,
                    Action = "unblocked",
                    Detail = "Círculo desbloqueado pela equipe do app.",
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        db.AdminActions.Add(new AdminAction
        {
            Id = Guid.NewGuid(),
            AdminUserId = admin.Id,
            Action = request.Blocked ? "block" : "unblock",
            TargetKind = kind,
            TargetId = request.TargetId,
            Detail = request.Blocked ? "Bloqueado." : "Desbloqueado.",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task<User?> RequireAdmin(CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == User.GetUserId(), cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (adminOptions.Value.Matches(user.Email) && !user.IsSystemAdmin)
        {
            user.IsSystemAdmin = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return user.IsSystemAdmin ? user : null;
    }
}
