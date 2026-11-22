using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Circulos.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private string? _databaseName;
    private string? _adminConnectionString;
    private string? _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        CreateDatabase();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Encryption:Key", TestEncryptionKey.Base64);
        builder.UseSetting("Database:Provider", "Postgres");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
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

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_connectionString);
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
        db.Database.Migrate();
        base.ConfigureClient(client);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DropDatabase();
        }
    }

    private void CreateDatabase()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "127.0.0.1";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "circulos";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "circulos";
        _databaseName = "circulos_test_" + Guid.NewGuid().ToString("N");
        _adminConnectionString = $"Host={host};Port={port};Database=postgres;Username={user};Password={password}";
        _connectionString = $"Host={host};Port={port};Database={_databaseName};Username={user};Password={password}";

        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\" TEMPLATE template0";
        command.ExecuteNonQuery();
    }

    private void DropDatabase()
    {
        if (_databaseName is null || _adminConnectionString is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();
        using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()";
            terminate.Parameters.AddWithValue("name", _databaseName);
            terminate.ExecuteNonQuery();
        }

        using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
        drop.ExecuteNonQuery();
        _databaseName = null;
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
