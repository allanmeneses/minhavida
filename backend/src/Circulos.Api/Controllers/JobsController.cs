using Circulos.Api.Contracts;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/jobs")]
public sealed class JobsController(JobQueue queue, OwnershipOfferService offers, CircleArchiveService archive) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<JobQueuedResponse>> Enqueue(CancellationToken cancellationToken)
    {
        var job = await queue.EnqueueAsync("work", "{}", cancellationToken);
        await offers.ProcessDueAsync(cancellationToken);
        await archive.ArchiveExpiredAsync(cancellationToken);
        await queue.ProcessDueAsync(cancellationToken);
        return Ok(new JobQueuedResponse(job.Id, job.Kind, "done"));
    }
}
