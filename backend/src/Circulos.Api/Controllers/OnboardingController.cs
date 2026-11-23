using Circulos.Api.Contracts;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/onboarding")]
public sealed class OnboardingController(OnboardingService onboarding) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OnboardingExampleResponse>>> List(CancellationToken cancellationToken)
    {
        var snapshot = await onboarding.SnapshotAsync(User.GetUserId(), cancellationToken);
        return Ok(snapshot.Examples.Select(item => new OnboardingExampleResponse(
            item.Id,
            item.CircleId,
            item.CircleId is Guid id && snapshot.CircleNames.TryGetValue(id, out var name) ? name : null,
            item.Kind,
            item.Title,
            item.Body,
            item.ConfirmedAt is null && item.DiscardedAt is null)).ToList());
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Decide(
        Guid id,
        [FromBody] OnboardingDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var error = await onboarding.DecideAsync(User.GetUserId(), id, request.Confirm, cancellationToken);
        return error is null ? NoContent() : NotFound(new ErrorResponse(error));
    }
}
