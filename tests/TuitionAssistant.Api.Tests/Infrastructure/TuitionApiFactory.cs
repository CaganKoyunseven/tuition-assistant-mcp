using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory with the PostgreSQL registration replaced by SQLite in-memory
/// (test double only) and the demo seed data loaded. One instance per test class.
/// </summary>
public class TuitionApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.UseSetting("Auth:BcryptWorkFactor", "4");

        builder.ConfigureServices(services =>
        {
            // Remove the Npgsql registration made in Program.cs.
            services.RemoveAll<DbContextOptions<TuitionDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TuitionDbContext>>();

            _connection.Open();
            services.AddDbContext<TuitionDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        EnsureDatabase();
    }

    private bool _initialized;

    /// <summary>Creates the schema and seed data once per factory.</summary>
    public void EnsureDatabase()
    {
        if (_initialized)
        {
            return;
        }

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TuitionDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync().GetAwaiter().GetResult();
        _initialized = true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
