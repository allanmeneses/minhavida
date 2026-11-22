using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IEmailSender email,
    AccountDeletionService deletion) : ControllerBase
{
    [HttpPost("deletion-requests")]
    public async Task<IActionResult> RequestDeletion(
        [FromBody] DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstAsync(x => x.Id == User.GetUserId(), cancellationToken);
        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ErrorResponse("Senha incorreta."));
        }

        var token = TokenHash.NewToken();
        db.AccountDeletionRequests.Add(new AccountDeletionRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHash.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await email.SendAsync(
            user.Email,
            "Confirme a exclusao da sua conta no Circulos",
            $"Para apagar seus dados, use este codigo: {token}",
            cancellationToken);
        return NoContent();
    }

    [HttpPost("deletion-requests/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmDeletion(
        [FromBody] ConfirmTokenRequest request,
        CancellationToken cancellationToken)
    {
        var hash = TokenHash.Hash(request.Token);
        var pending = await db.AccountDeletionRequests
            .Include(x => x.User)
            .ThenInclude(x => x.Profile)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (pending is null || pending.UsedAt is not null || pending.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ErrorResponse("Esta confirmacao nao e valida."));
        }

        pending.UsedAt = DateTime.UtcNow;
        await deletion.DeleteAsync(pending.User, cancellationToken);
        return NoContent();
    }
}
