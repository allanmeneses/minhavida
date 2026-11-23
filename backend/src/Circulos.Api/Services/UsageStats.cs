using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class UsageStats(AppDbContext db)
{
    public const string ShortcutRun = "shortcut_run";
    public const string EntryCreated = "entry_created";

    public Task IncrementAsync(string metric, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO usage_counters (""Metric"", ""Count"") VALUES ({metric}, 1)
               ON CONFLICT (""Metric"") DO UPDATE SET ""Count"" = usage_counters.""Count"" + 1",
            cancellationToken);

    public async Task<(long Shortcuts, long Entries, long ActiveCircles)> SnapshotAsync(CancellationToken cancellationToken)
    {
        var rows = await db.UsageCounters.AsNoTracking().ToListAsync(cancellationToken);
        var shortcuts = rows.FirstOrDefault(item => item.Metric == ShortcutRun)?.Count ?? 0;
        var entries = rows.FirstOrDefault(item => item.Metric == EntryCreated)?.Count ?? 0;
        var active = await db.Circles.CountAsync(
            circle => circle.Status == CircleStatus.Active,
            cancellationToken);
        return (shortcuts, entries, active);
    }
}
