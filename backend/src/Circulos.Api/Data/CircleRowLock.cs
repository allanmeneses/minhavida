using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Data;

public static class CircleRowLock
{
    public static Task AcquireAsync(AppDbContext db, Guid circleId, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Task.CompletedTask;
        }

        return db.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT 1 FROM circles WHERE ""Id"" = {circleId} FOR UPDATE",
            cancellationToken);
    }
}
