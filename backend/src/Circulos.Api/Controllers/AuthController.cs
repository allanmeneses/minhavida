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
using Microsoft.Extensions.Options;
using Npgsql;

namespace Circulos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    SessionService sessions,
    IEmailSender email,
    TimeProvider clock,
    IWebHostEnvironment env,
    JwtTokenService jwt,
    OnboardingService onboarding,
    IOptions<GoogleAuthOptions> googleOptions,
    IGoogleIdTokenValidator googleTokens) : ControllerBase
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
                return Conflict(new ErrorResponse("Já existe uma conta com este e-mail."));
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
            onboarding.Seed(user, now);
            await db.SaveChangesAsync(cancellationToken);
            return Created("/api/auth/me", AttachWebCookie(await sessions.IssueAsync(user, cancellationToken)));
        }
        catch (Exception ex) when (IsEmailUniqueViolation(ex) || IsBusyWrite(ex))
        {
            return Conflict(new ErrorResponse("Já existe uma conta com este e-mail."));
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

        if (user.BlockedAt is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta conta foi bloqueada."));
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
        return Ok(AttachWebCookie(issued));
    }

    [HttpGet("google")]
    [AllowAnonymous]
    public ActionResult<GoogleAuthStatusResponse> GoogleStatus() =>
        Ok(new GoogleAuthStatusResponse(googleOptions.Value.Enabled && !string.IsNullOrWhiteSpace(googleOptions.Value.ClientId)));

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Google(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!googleOptions.Value.Enabled)
        {
            return NotFound(new ErrorResponse("Login com Google está desligado."));
        }

        if (string.IsNullOrWhiteSpace(googleOptions.Value.ClientId))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("Login com Google não está configurado."));
        }

        var identity = await googleTokens.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return Unauthorized(new ErrorResponse("Não foi possível validar o Google."));
        }

        var user = await db.Users.Include(item => item.Profile)
            .FirstOrDefaultAsync(item => item.GoogleSubject == identity.Subject, cancellationToken);
        if (user is null)
        {
            user = await db.Users.Include(item => item.Profile)
                .FirstOrDefaultAsync(item => item.Email == identity.Email, cancellationToken);
            if (user is not null)
            {
                return Conflict(new ErrorResponse("Já existe uma conta com este e-mail. Entre com senha."));
            }

            var now = clock.GetUtcNow().UtcDateTime;
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = identity.Email,
                CreatedAt = now,
                GoogleSubject = identity.Subject,
                Profile = new Profile
                {
                    DisplayName = identity.Name,
                    IsPublic = false,
                    UpdatedAt = now,
                },
            };
            user.PasswordHash = passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N") + "Aa1!");
            foreach (var area in LifeAreaDefaults.ForUser(user.Id, now))
            {
                user.LifeAreas.Add(area);
            }

            db.Users.Add(user);
            onboarding.Seed(user, now);
            await db.SaveChangesAsync(cancellationToken);
            return Created("/api/auth/me", AttachWebCookie(await sessions.IssueAsync(user, cancellationToken)));
        }

        if (user.BlockedAt is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta conta foi bloqueada."));
        }

        return Ok(AttachWebCookie(await sessions.IssueAsync(user, cancellationToken)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest? request,
        CancellationToken cancellationToken)
    {
        var token = FirstRefresh(request?.RefreshToken, AuthCookie.Read(Request));
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new ErrorResponse("Sessão expirada. Entre de novo."));
        }

        var rotated = await sessions.RotateAsync(token, cancellationToken);
        return rotated is null
            ? Unauthorized(new ErrorResponse("Sessão expirada. Entre de novo."))
            : Ok(AttachWebCookie(rotated));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var token = FirstRefresh(request?.RefreshToken, AuthCookie.Read(Request));
        if (!string.IsNullOrWhiteSpace(token))
        {
            await sessions.RevokeAsync(token, cancellationToken);
        }

        if (AuthCookie.IsWeb(Request))
        {
            AuthCookie.Clear(Response, env.IsProduction());
        }

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
                "Redefinir senha do Círculos",
                $"Use este código para redefinir a senha: {token}",
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
            return BadRequest(new ErrorResponse("Este link de redefinição não é válido."));
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
            return Unauthorized(new ErrorResponse("Sessão inválida."));
        }

        return Ok(new AuthUserDto(user.Id, user.Email, user.Profile.DisplayName, user.IsSystemAdmin));
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
            return BadRequest(new ErrorResponse("O segundo fator já está ativo."));
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
            return BadRequest(new ErrorResponse("O segundo fator já está ativo."));
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret)
            || string.IsNullOrWhiteSpace(request.Code)
            || !Totp.Verify(user.TwoFactorSecret, request.Code, clock.GetUtcNow()))
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new ErrorResponse("Código do autenticador inválido."));
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
            return BadRequest(new ErrorResponse("Código do autenticador inválido."));
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
            return Unauthorized(new ErrorResponse("Código expirado. Entre de novo."));
        }

        await UserRowLock.AcquireAsync(db, challenge.UserId, cancellationToken);
        await db.Entry(challenge).ReloadAsync(cancellationToken);
        if (challenge.UsedAt is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Unauthorized(new ErrorResponse("Código expirado. Entre de novo."));
        }

        if (!await AcceptFactorAsync(challenge.User, request.Code, request.RecoveryCode, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Unauthorized(new ErrorResponse("Código do autenticador inválido."));
        }

        challenge.UsedAt = clock.GetUtcNow().UtcDateTime;
        if (challenge.User.BlockedAt is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Esta conta foi bloqueada."));
        }

        var issued = await sessions.IssueAsync(challenge.User, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(AttachWebCookie(issued));
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

    private AuthResponse AttachWebCookie(AuthResponse issued)
    {
        if (!AuthCookie.IsWeb(Request) || string.IsNullOrWhiteSpace(issued.RefreshToken))
        {
            return issued;
        }

        AuthCookie.Set(Response, issued.RefreshToken, env.IsProduction(), jwt.RefreshDays());
        return issued;
    }

    private static string? FirstRefresh(string? body, string? cookie) =>
        !string.IsNullOrWhiteSpace(body) ? body : cookie;

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
            throw new InvalidOperationException("Usuário não autenticado.");
        }

        return id;
    }
}
