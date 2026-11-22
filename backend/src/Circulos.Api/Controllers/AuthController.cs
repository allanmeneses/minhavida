using System.Security.Claims;
using Circulos.Api.Auth;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Circulos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    SessionService sessions,
    IEmailSender email,
    TimeProvider clock) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(request.Email);
        try
        {
            var exists = await db.Users.AnyAsync(x => x.Email == normalized, cancellationToken);
            if (exists)
            {
                return Conflict(new ErrorResponse("Ja existe uma conta com este e-mail."));
            }

            var now = clock.GetUtcNow().UtcDateTime;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalized,
                CreatedAt = now,
                Profile = new Profile
                {
                    DisplayName = request.Name.Trim(),
                    IsPublic = false,
                    UpdatedAt = now,
                },
            };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            foreach (var area in LifeAreaDefaults.ForUser(user.Id, now))
            {
                user.LifeAreas.Add(area);
            }

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return Created("/api/auth/me", await sessions.IssueAsync(user, cancellationToken));
        }
        catch (Exception ex) when (IsEmailUniqueViolation(ex) || IsBusyWrite(ex))
        {
            return Conflict(new ErrorResponse("Ja existe uma conta com este e-mail."));
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(request.Email);
        var user = await db.Users
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ErrorResponse("E-mail ou senha incorretos."));
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ErrorResponse("E-mail ou senha incorretos."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, user.Id, cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);
        if (user.TwoFactorEnabled)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var challenge = TokenHash.NewToken();
            db.TwoFactorChallenges.Add(new TwoFactorChallenge
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = TokenHash.Hash(challenge),
                ExpiresAt = now.AddMinutes(5),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new AuthResponse(null, null, null, true, challenge));
        }

        var issued = await sessions.IssueAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(issued);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var rotated = await sessions.RotateAsync(request.RefreshToken, cancellationToken);
        return rotated is null
            ? Unauthorized(new ErrorResponse("Sessao expirada. Entre de novo."))
            : Ok(rotated);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await sessions.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
        if (user is not null)
        {
            var token = TokenHash.NewToken();
            db.PasswordResets.Add(new PasswordReset
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = TokenHash.Hash(token),
                ExpiresAt = clock.GetUtcNow().UtcDateTime.AddHours(2),
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(cancellationToken);
            await email.SendAsync(
                user.Email,
                "Redefinir senha do Circulos",
                $"Use este codigo para redefinir a senha: {token}",
                cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var hash = TokenHash.Hash(request.Token);
        var reset = await db.PasswordResets
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (reset is null || reset.UsedAt is not null || reset.ExpiresAt < clock.GetUtcNow().UtcDateTime)
        {
            return BadRequest(new ErrorResponse("Este link de redefinicao nao e valido."));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, reset.UserId, cancellationToken);
        reset.UsedAt = clock.GetUtcNow().UtcDateTime;
        reset.User.PasswordHash = passwordHasher.HashPassword(reset.User, request.Password);
        await db.PasswordResets
            .Where(x => x.UserId == reset.UserId && x.Id != reset.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, clock.GetUtcNow().UtcDateTime), cancellationToken);
        await sessions.RevokeAllAsync(reset.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await db.Users
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Sessao invalida."));
        }

        return Ok(new AuthUserDto(user.Id, user.Email, user.Profile.DisplayName));
    }

    [HttpGet("2fa")]
    [Authorize]
    public async Task<ActionResult<TwoFactorStatusResponse>> TwoFactorStatus(CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstAsync(x => x.Id == User.GetUserId(), cancellationToken);
        return Ok(new TwoFactorStatusResponse(user.TwoFactorEnabled));
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<TwoFactorSetupResponse>> SetupTwoFactor(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        if (user.TwoFactorEnabled)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new ErrorResponse("O segundo fator ja esta ativo."));
        }

        var secret = Totp.NewSecret();
        var codes = Totp.NewRecoveryCodes();
        user.TwoFactorSecret = secret;
        await db.TwoFactorRecoveryCodes.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        foreach (var code in codes)
        {
            db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = TokenHash.Hash(Totp.NormalizeCode(code)),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var uri = Totp.OtpauthUri(user.Email, secret);
        return Ok(new TwoFactorSetupResponse(secret, uri, Convert.ToBase64String(TotpQr.Png(uri)), codes));
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor(
        [FromBody] TwoFactorCodeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        if (user.TwoFactorEnabled)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new ErrorResponse("O segundo fator ja esta ativo."));
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret)
            || string.IsNullOrWhiteSpace(request.Code)
            || !Totp.Verify(user.TwoFactorSecret, request.Code, clock.GetUtcNow()))
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new ErrorResponse("Codigo do autenticador invalido."));
        }

        user.TwoFactorEnabled = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor(
        [FromBody] TwoFactorDisableRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, userId, cancellationToken);
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        var password = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (password == PasswordVerificationResult.Failed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Unauthorized(new ErrorResponse("Senha incorreta."));
        }

        if (!user.TwoFactorEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }

        if (!await AcceptFactorAsync(user, request.Code, request.RecoveryCode, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new ErrorResponse("Codigo do autenticador invalido."));
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await db.TwoFactorRecoveryCodes.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> VerifyTwoFactor(
        [FromBody] TwoFactorVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var hash = TokenHash.Hash(request.TwoFactorToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var challenge = await db.TwoFactorChallenges
            .Include(x => x.User)
            .ThenInclude(x => x.Profile)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (challenge is null || challenge.UsedAt is not null || challenge.ExpiresAt < clock.GetUtcNow().UtcDateTime)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Unauthorized(new ErrorResponse("Codigo expirado. Entre de novo."));
        }

        await UserRowLock.AcquireAsync(db, challenge.UserId, cancellationToken);
        await db.Entry(challenge).ReloadAsync(cancellationToken);
        if (challenge.UsedAt is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Unauthorized(new ErrorResponse("Codigo expirado. Entre de novo."));
        }

        if (!await AcceptFactorAsync(challenge.User, request.Code, request.RecoveryCode, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Unauthorized(new ErrorResponse("Codigo do autenticador invalido."));
        }

        challenge.UsedAt = clock.GetUtcNow().UtcDateTime;
        var issued = await sessions.IssueAsync(challenge.User, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(issued);
    }

    private async Task<bool> AcceptFactorAsync(
        User user,
        string? code,
        string? recoveryCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(code)
            && !string.IsNullOrWhiteSpace(user.TwoFactorSecret)
            && Totp.Verify(user.TwoFactorSecret, code, clock.GetUtcNow()))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(recoveryCode))
        {
            return false;
        }

        var hash = TokenHash.Hash(Totp.NormalizeCode(recoveryCode));
        var stored = await db.TwoFactorRecoveryCodes
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.CodeHash == hash && x.UsedAt == null, cancellationToken);
        if (stored is null)
        {
            return false;
        }

        stored.UsedAt = clock.GetUtcNow().UtcDateTime;
        return true;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsEmailUniqueViolation(Exception exception)
    {
        if (exception is DbUpdateException update && update.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_users_Email", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBusyWrite(Exception exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
            || message.Contains("active statements", StringComparison.OrdinalIgnoreCase);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var id))
        {
            throw new InvalidOperationException("Usuario nao autenticado.");
        }

        return id;
    }
}
