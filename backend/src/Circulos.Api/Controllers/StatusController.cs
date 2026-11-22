using Circulos.Api.Contracts;
using Circulos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Circulos.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class StatusController(StatusPage status) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Live() => Ok(new { status = "ok" });

    [HttpGet("/health/ready")]
    public async Task<ActionResult<StatusResponse>> Ready(CancellationToken cancellationToken)
    {
        var snapshot = await status.SnapshotAsync(cancellationToken);
        if (snapshot.Database != "ok")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new StatusResponse(
                snapshot.Api,
                snapshot.Database,
                snapshot.Telemetry,
                snapshot.CheckedAt));
        }

        return Ok(new StatusResponse(snapshot.Api, snapshot.Database, snapshot.Telemetry, snapshot.CheckedAt));
    }

    [HttpGet("/api/status")]
    public async Task<ActionResult<StatusResponse>> Json(CancellationToken cancellationToken)
    {
        var snapshot = await status.SnapshotAsync(cancellationToken);
        return Ok(new StatusResponse(snapshot.Api, snapshot.Database, snapshot.Telemetry, snapshot.CheckedAt));
    }

    [HttpGet("/status")]
    public async Task<IActionResult> Page(CancellationToken cancellationToken)
    {
        var snapshot = await status.SnapshotAsync(cancellationToken);
        return Content(StatusPage.Html(snapshot), "text/html; charset=utf-8");
    }
}
