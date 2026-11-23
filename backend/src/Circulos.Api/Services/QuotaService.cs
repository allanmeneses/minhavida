using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class QuotaService(AppDbContext db, TimeProvider clock)
{
    public async Task<string?> TryAddStorageAsync(Guid userId, long bytes, CancellationToken cancellationToken)
    {
        if (bytes <= 0)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.Users
            .Where(user => user.Id == userId
                && (user.StorageUsedBytes + bytes <= PlanLimits.FreeStorageBytes
                    || (user.Plan == PlanKind.Paid
                        && (user.SubscriptionStatus == SubscriptionStatus.Active
                            || (user.SubscriptionStatus == SubscriptionStatus.Cancelled && user.PaidUntil != null && user.PaidUntil > now))
                        && user.StorageUsedBytes + bytes <= PlanLimits.PaidStorageBytes)))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.StorageUsedBytes, user => user.StorageUsedBytes + bytes),
                cancellationToken);

        if (rows == 1)
        {
            return null;
        }

        var exists = await db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        return exists
            ? "Você atingiu o limite de armazenamento do plano."
            : "Conta não encontrada.";
    }

    public async Task ReleaseStorageAsync(Guid userId, long bytes, CancellationToken cancellationToken)
    {
        if (bytes <= 0)
        {
            return;
        }

        await db.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    user => user.StorageUsedBytes,
                    user => user.StorageUsedBytes < bytes ? 0 : user.StorageUsedBytes - bytes),
                cancellationToken);
    }

    public async Task<string?> TryAddCircleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.Users
            .Where(user => user.Id == userId
                && (user.OwnedCircleCount < PlanLimits.FreeCircleLimit
                    || (user.Plan == PlanKind.Paid
                        && (user.SubscriptionStatus == SubscriptionStatus.Active
                            || (user.SubscriptionStatus == SubscriptionStatus.Cancelled && user.PaidUntil != null && user.PaidUntil > now)))))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.OwnedCircleCount, user => user.OwnedCircleCount + 1),
                cancellationToken);

        if (rows == 1)
        {
            return null;
        }

        var exists = await db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        return exists
            ? "O plano gratuito inclui até 3 círculos."
            : "Conta não encontrada.";
    }

    public Task ReleaseCircleAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users
            .Where(user => user.Id == userId && user.OwnedCircleCount > 0)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.OwnedCircleCount, user => user.OwnedCircleCount - 1),
                cancellationToken);

    public async Task<bool> BenefitsActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is not null && PlanLimits.BenefitsActive(user, clock.GetUtcNow().UtcDateTime);
    }
}
