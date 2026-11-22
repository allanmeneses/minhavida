using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Circulos.Api.Data;

public static class UserRowLock
{
    public static Task AcquireAsync(AppDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Task.CompletedTask;
        }

        return db.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT 1 FROM users WHERE ""Id"" = {userId} FOR UPDATE",
            cancellationToken);
    }

    public static bool IsMissingUser(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && (postgres.SqlState == PostgresErrorCodes.ForeignKeyViolation
                    || postgres.SqlState == PostgresErrorCodes.NoDataFound))
            {
                return true;
            }
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("23503", StringComparison.Ordinal);
    }
}
