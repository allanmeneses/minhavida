using Circulos.Api.Contracts;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/circles/{id:guid}/ownership-offer")]
public sealed class OwnershipOfferController(OwnershipOfferService offers) : ControllerBase
{
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var (status, error) = await offers.AcceptAsync(id, User.GetUserId(), cancellationToken);
        if (error is not null)
        {
            return StatusCode(status, new ErrorResponse(error));
        }

        return NoContent();
    }
}
