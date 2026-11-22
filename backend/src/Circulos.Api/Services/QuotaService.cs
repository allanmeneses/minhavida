using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class QuotaService(AppDbContext db)
{
    public async Task<string?> TryAddStorageAsync(Guid userId, long bytes, CancellationToken cancellationToken)
    {
        if (bytes <= 0)
        {
            return null;
        }

        var rows = await db.Users
            .Where(user => user.Id == userId
                && user.StorageUsedBytes + bytes <= (user.Plan == PlanKind.Paid ? PlanLimits.PaidStorageBytes : PlanLimits.FreeStorageBytes))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.StorageUsedBytes, user => user.StorageUsedBytes + bytes),
                cancellationToken);

        if (rows == 1)
        {
            return null;
        }

        var exists = await db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        return exists
            ? "Voce atingiu o limite de armazenamento do plano."
            : "Conta nao encontrada.";
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
        var rows = await db.Users
            .Where(user => user.Id == userId
                && (user.Plan == PlanKind.Paid || user.OwnedCircleCount < PlanLimits.FreeCircleLimit))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.OwnedCircleCount, user => user.OwnedCircleCount + 1),
                cancellationToken);

        if (rows == 1)
        {
            return null;
        }

        var exists = await db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        return exists
            ? "O plano gratuito inclui ate 3 circulos."
            : "Conta nao encontrada.";
    }

    public Task ReleaseCircleAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users
            .Where(user => user.Id == userId && user.OwnedCircleCount > 0)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.OwnedCircleCount, user => user.OwnedCircleCount - 1),
                cancellationToken);
}
