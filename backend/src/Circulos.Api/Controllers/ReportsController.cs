using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(AppDbContext db) : ControllerBase
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

        var report = new ContentReport
        {
            Id = Guid.NewGuid(),
            ReporterUserId = User.GetUserId(),
            TargetKind = kind,
            TargetId = request.TargetId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.ContentReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
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
