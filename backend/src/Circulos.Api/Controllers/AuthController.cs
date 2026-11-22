using System.Security.Claims;
using Circulos.Api.Auth;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
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
    JwtTokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var exists = await db.Users.AnyAsync(x => x.Email == email, cancellationToken);
        if (exists)
        {
            return Conflict(new ErrorResponse("Ja existe uma conta com este e-mail."));
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAt = now,
            Profile = new Profile
            {
                DisplayName = request.Name.Trim(),
                UpdatedAt = now,
            },
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsEmailUniqueViolation(ex))
        {
            return Conflict(new ErrorResponse("Ja existe uma conta com este e-mail."));
        }

        return Created("/api/auth/me", ToResponse(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

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

    private static bool IsEmailUniqueViolation(DbUpdateException exception)
    {
        if (exception.InnerException is Npgsql.PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_users_Email", StringComparison.OrdinalIgnoreCase);
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
