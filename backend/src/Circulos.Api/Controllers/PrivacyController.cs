using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/privacy")]
public sealed class PrivacyController(AppDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<PrivacyOverviewResponse>> Mine(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await db.Profiles.AsNoTracking().FirstAsync(item => item.UserId == userId, cancellationToken);
        var circles = await db.CircleMembers.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new { item.Circle.Name, item.Circle.Visibility, item.Role, item.Circle.Status })
            .ToListAsync(cancellationToken);
        var items = new List<PrivacyItemResponse>
        {
            new("Diario, documentos e vacinas", "So voce. Ninguem do circulo ve isso sozinho."),
            new(
                "Perfil",
                profile.IsPublic
                    ? "Quem tiver o link ve nome, foto e descricao. Diario, telefone e e-mail ficam de fora."
                    : "Privado. Ninguem de fora ve seu nome, foto ou descricao."),
        };
        foreach (var circle in circles.Where(item => item.Status != CircleStatus.Deleted))
        {
            var who = circle.Visibility == CircleVisibility.Public
                ? "Quem nao entrou ve so o nome e a descricao do circulo. Quem entrou ve recados e o log."
                : "So quem esta neste circulo ve o que acontece la.";
            items.Add(new PrivacyItemResponse($"Circulo {circle.Name}", who));
        }

        return Ok(new PrivacyOverviewResponse(items));
    }
}
