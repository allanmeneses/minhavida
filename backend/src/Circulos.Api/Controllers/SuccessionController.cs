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
[Route("api/circles/{id:guid}/succession")]
public sealed class SuccessionController(AppDbContext db, SuccessionService succession) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SuccessionVoteResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await db.Circles.Include(item => item.Members).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted || circle.Members.All(member => member.UserId != userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var vote = await succession.CurrentAsync(id, cancellationToken);
        if (vote is null)
        {
            return NotFound(new ErrorResponse("Nao ha votacao aberta."));
        }

        return Ok(ToResponse(vote, circle.Members.Count, userId));
    }

    [HttpPost("votes")]
    public async Task<IActionResult> Vote(
        Guid id,
        [FromBody] SuccessionVoteRequest request,
        CancellationToken cancellationToken)
    {
        var (status, error) = await succession.CastAsync(id, User.GetUserId(), request.CandidateUserId, cancellationToken);
        if (error is not null)
        {
            return StatusCode(status, new ErrorResponse(error));
        }

        return NoContent();
    }

    private static SuccessionVoteResponse ToResponse(SuccessionVote vote, int memberCount, Guid userId) =>
        new(
            vote.Id,
            vote.Status switch
            {
                SuccessionVoteStatus.Decided => "decided",
                SuccessionVoteStatus.Expired => "expired",
                _ => "open",
            },
            vote.OpenedAt,
            vote.DeadlineAt,
            vote.WinnerUserId,
            memberCount,
            vote.Ballots
                .GroupBy(ballot => ballot.CandidateUserId)
                .Select(group => new SuccessionTallyResponse(group.Key, group.Count()))
                .OrderByDescending(item => item.Votes)
                .ToList(),
            vote.Ballots.FirstOrDefault(ballot => ballot.VoterUserId == userId)?.CandidateUserId);
}
