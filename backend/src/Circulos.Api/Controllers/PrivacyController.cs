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
            new("Diário, documentos e vacinas", "Só você. Ninguém do círculo vê isso sozinho."),
            new(
                "Perfil",
                profile.IsPublic
                    ? "Quem tiver o link vê nome, foto e descrição. Diário, telefone e e-mail ficam de fora."
                    : "Privado. Ninguém de fora vê seu nome, foto ou descrição."),
        };
        foreach (var circle in circles.Where(item => item.Status != CircleStatus.Deleted))
        {
            var who = circle.Visibility == CircleVisibility.Public
                ? "Quem não entrou vê só o nome e a descrição do círculo. Quem entrou vê recados e o log."
                : "Só quem está neste círculo vê o que acontece lá.";
            items.Add(new PrivacyItemResponse($"Círculo {circle.Name}", who));
        }

        return Ok(new PrivacyOverviewResponse(items));
    }
}
