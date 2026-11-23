using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class JobQueue(AppDbContext db, TimeProvider clock)
{
    public async Task<BackgroundJob> EnqueueAsync(string kind, string payload, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Payload = payload,
            Status = "pending",
            RunAt = now,
            CreatedAt = now,
        };
        db.BackgroundJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var pending = await db.BackgroundJobs
            .Where(job => job.Status == "pending" && job.RunAt <= now)
            .OrderBy(job => job.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        var done = 0;
        foreach (var job in pending)
        {
            job.Status = "done";
            job.CompletedAt = now;
            done += 1;
        }

        if (done > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return done;
    }
}

public sealed class JobWorker(IServiceScopeFactory scopes, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var offers = scope.ServiceProvider.GetRequiredService<OwnershipOfferService>();
                var archive = scope.ServiceProvider.GetRequiredService<CircleArchiveService>();
                var queue = scope.ServiceProvider.GetRequiredService<JobQueue>();
                await offers.ProcessDueAsync(stoppingToken);
                await archive.ArchiveExpiredAsync(stoppingToken);
                await queue.ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // o worker tenta de novo no proximo ciclo
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _ = clock;
    }
}
