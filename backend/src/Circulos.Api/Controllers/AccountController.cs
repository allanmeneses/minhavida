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
    [AllowAnonymous]
    [HttpPost("appeals")]
    public async Task<IActionResult> Appeal(
        [FromBody] AppealRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == normalized, cancellationToken);
        if (user is null)
        {
            return NoContent();
        }

        var check = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ErrorResponse("E-mail ou senha incorretos."));
        }

        if (user.BlockedAt is null)
        {
            return BadRequest(new ErrorResponse("Esta conta não está bloqueada."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, user.Id, cancellationToken);
        var open = await db.AccountAppeals.AnyAsync(
            item => item.UserId == user.Id && item.Status == "open",
            cancellationToken);
        if (!open)
        {
            db.AccountAppeals.Add(new AccountAppeal
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Note = request.Note.Trim(),
                Status = "open",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("support")]
    public async Task<IActionResult> Support(
        [FromBody] SupportRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstAsync(item => item.Id == User.GetUserId(), cancellationToken);
        db.SupportMessages.Add(new SupportMessage
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = user.Email,
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await email.SendAsync(
            user.Email,
            "Recebemos seu pedido de suporte",
            "A equipe vai ler sua mensagem. Não há prazo prometido de resposta.",
            cancellationToken);
        return NoContent();
    }

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
            "Confirme a exclusão da sua conta no Círculos",
            $"Para apagar seus dados, use este código: {token}",
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
            return BadRequest(new ErrorResponse("Esta confirmação não é válida."));
        }

        pending.UsedAt = DateTime.UtcNow;
        await deletion.DeleteAsync(pending.User, cancellationToken);
        return NoContent();
    }
}
