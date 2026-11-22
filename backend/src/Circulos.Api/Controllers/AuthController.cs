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
    JwtTokenService tokens,
    IEmailSender email) : ControllerBase
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

            var now = DateTime.UtcNow;
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
            return Created("/api/auth/me", ToResponse(user));
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

        return Ok(ToResponse(user));
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
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow,
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
        if (reset is null || reset.UsedAt is not null || reset.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ErrorResponse("Este link de redefinicao nao e valido."));
        }

        reset.UsedAt = DateTime.UtcNow;
        reset.User.PasswordHash = passwordHasher.HashPassword(reset.User, request.Password);
        await db.PasswordResets
            .Where(x => x.UserId == reset.UserId && x.Id != reset.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, DateTime.UtcNow), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
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

    private AuthResponse ToResponse(User user) =>
        new(tokens.Create(user), new AuthUserDto(user.Id, user.Email, user.Profile.DisplayName));

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
