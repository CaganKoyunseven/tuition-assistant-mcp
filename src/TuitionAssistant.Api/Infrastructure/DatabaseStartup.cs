using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TuitionAssistant.Api.Infrastructure;

/// <summary>Applies migrations and seed data at startup when enabled in configuration.</summary>
public static class DatabaseStartup
{
    private const int MaxMigrationAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.MigrateOnStartup && !options.SeedOnStartup)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseStartup));

        if (options.MigrateOnStartup)
        {
            var db = scope.ServiceProvider.GetRequiredService<TuitionDbContext>();
            await MigrateWithRetryAsync(db, logger);
        }

        if (options.SeedOnStartup)
        {
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
            logger.LogInformation("Seed data verified");
        }
    }

    // The database container (or Azure server) may still be starting when the API boots.
    private static async Task MigrateWithRetryAsync(TuitionDbContext db, ILogger logger)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied");
                return;
            }
            catch (Exception ex) when (attempt < MaxMigrationAttempts && ex is not InvalidOperationException)
            {
                logger.LogWarning("Database not reachable (attempt {Attempt}/{Max}): {Message}", attempt, MaxMigrationAttempts, ex.Message);
                await Task.Delay(RetryDelay);
            }
        }
    }
}
