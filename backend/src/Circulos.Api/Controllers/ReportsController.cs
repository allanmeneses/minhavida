using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(AppDbContext db, IEmailSender email) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReportCreatedResponse>> Create(
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken)
    {
        var kind = NormalizeKind(request.TargetKind);
        if (kind is null)
        {
            return BadRequest(new ErrorResponse("Denuncie um perfil, um circulo ou um post."));
        }

        if (!await TargetExists(kind, request.TargetId, cancellationToken))
        {
            return NotFound(new ErrorResponse("Alvo da denuncia nao encontrado."));
        }

        Guid? circleId = kind switch
        {
            "circle" => request.TargetId,
            "post" => await db.CirclePosts.AsNoTracking()
                .Where(post => post.Id == request.TargetId)
                .Select(post => (Guid?)post.CircleId)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null,
        };
        var report = new ContentReport
        {
            Id = Guid.NewGuid(),
            ReporterUserId = User.GetUserId(),
            TargetKind = kind,
            TargetId = request.TargetId,
            CircleId = circleId,
            Status = "open",
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.ContentReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        if (circleId is Guid cid)
        {
            var admins = await (
                    from member in db.CircleMembers.AsNoTracking()
                    join person in db.Users.AsNoTracking() on member.UserId equals person.Id
                    where member.CircleId == cid && member.Role >= CircleRole.Admin
                    select person.Email)
                .ToListAsync(cancellationToken);
            foreach (var to in admins.Distinct())
            {
                await email.SendAsync(
                    to,
                    "Denuncia no seu circulo",
                    "Alguem denunciou um conteudo neste circulo. Abra o circulo para decidir se mantem ou esconde.",
                    cancellationToken);
            }
        }

        return Ok(new ReportCreatedResponse(report.Id));
    }

    private async Task<bool> TargetExists(string kind, Guid id, CancellationToken cancellationToken) =>
        kind switch
        {
            "profile" => await db.Users.AnyAsync(user => user.Id == id, cancellationToken),
            "circle" => await db.Circles.AnyAsync(circle => circle.Id == id && circle.Status != CircleStatus.Deleted, cancellationToken),
            "post" => await db.CirclePosts.AnyAsync(post => post.Id == id, cancellationToken),
            _ => false,
        };

    internal static string? NormalizeKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "profile" or "perfil" => "profile",
        "circle" or "circulo" => "circle",
        "post" => "post",
        _ => null,
    };
}
