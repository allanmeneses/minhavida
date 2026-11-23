using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Auth;

public sealed class SessionService(
    AppDbContext db,
    JwtTokenService tokens,
    TimeProvider clock,
    Microsoft.Extensions.Options.IOptions<Circulos.Api.Services.SystemAdminOptions> adminOptions)
{
    public async Task<AuthResponse> IssueAsync(User user, CancellationToken cancellationToken, Guid? familyId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var refresh = TokenHash.NewToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = familyId ?? Guid.NewGuid(),
            TokenHash = TokenHash.Hash(refresh),
            ExpiresAt = now.AddDays(tokens.RefreshDays()),
            CreatedAt = now,
        });
        PromoteIfAdmin(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToAuth(user, tokens.CreateAccess(user), refresh);
    }

    public async Task<AuthResponse?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Hash(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await UserRowLock.AcquireAsync(db, current.UserId, cancellationToken);
        await db.Entry(current).ReloadAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (current.RevokedAt is not null || current.ExpiresAt < now)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        current.RevokedAt = now;
        var user = await db.Users.Include(x => x.Profile).FirstAsync(x => x.Id == current.UserId, cancellationToken);
        var nextPlain = TokenHash.NewToken();
        var next = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = current.FamilyId,
            TokenHash = TokenHash.Hash(nextPlain),
            ExpiresAt = now.AddDays(tokens.RefreshDays()),
            CreatedAt = now,
        };
        current.ReplacedById = next.Id;
        db.RefreshTokens.Add(next);
        PromoteIfAdmin(user);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAuth(user, tokens.CreateAccess(user), nextPlain);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Hash(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await UserRowLock.AcquireAsync(db, current.UserId, cancellationToken);
        await db.Entry(current).ReloadAsync(cancellationToken);
        if (current.RevokedAt is null)
        {
            current.RevokedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    private void PromoteIfAdmin(User user)
    {
        if (adminOptions.Value.Matches(user.Email) && !user.IsSystemAdmin)
        {
            user.IsSystemAdmin = true;
        }
    }

    private static AuthResponse ToAuth(User user, string access, string refresh) =>
        new(access, refresh, new AuthUserDto(user.Id, user.Email, user.Profile.DisplayName, user.IsSystemAdmin), false, null);
}
