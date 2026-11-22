using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Encryption:Key", TestEncryptionKey.Base64);
        builder.ConfigureServices(services =>
        {
            RemoveService(services, typeof(DbContextOptions<AppDbContext>));
            RemoveService(services, typeof(AppDbContext));
            RemoveService(services, typeof(IFileStorage));
            RemoveService(services, typeof(IEncryptionKeyProvider));
            RemoveService(services, typeof(LocalFileStorage));
            RemoveService(services, typeof(EncryptedFileStorage));

            foreach (var descriptor in services
                .Where(d => d.ServiceType.FullName?.Contains("DbContext", StringComparison.Ordinal) == true
                            && (d.ServiceType.GenericTypeArguments.Any(t => t == typeof(AppDbContext))
                                || d.ImplementationType == typeof(AppDbContext)))
                .ToList())
            {
                services.Remove(descriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.AddInterceptors(new OverlapSaveInterceptor());
            });
            var inspectable = new InspectableMemoryFileStorage();
            services.AddSingleton(inspectable);
            services.AddSingleton<IEncryptionKeyProvider>(_ => new StaticEncryptionKeyProvider(TestEncryptionKey.Bytes));
            services.AddSingleton<IFileStorage>(sp =>
                new EncryptedFileStorage(sp.GetRequiredService<InspectableMemoryFileStorage>(), sp.GetRequiredService<IEncryptionKeyProvider>()));
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        base.ConfigureClient(client);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }

    private static void RemoveService(IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == serviceType).ToList())
        {
            services.Remove(descriptor);
        }
    }
}

/// <summary>
/// Atrasa o SaveChanges de cadastro para o teste de e-mail duplicado concorrente
/// conseguir sobrepor as duas escritas, nao so disparar uma depois que a outra ja commitou.
/// </summary>
internal sealed class OverlapSaveInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context?.ChangeTracker.Entries<User>().Any(entry => entry.State == EntityState.Added) == true)
        {
            await Task.Delay(80, cancellationToken);
        }

        return result;
    }
}

