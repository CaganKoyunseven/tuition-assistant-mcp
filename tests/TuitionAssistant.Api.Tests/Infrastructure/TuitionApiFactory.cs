using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory with the PostgreSQL registration replaced by SQLite in-memory
/// (test double only), a test RSA key and the demo seed data loaded. One instance per test class.
/// </summary>
public class TuitionApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Lock _initLock = new();
    private bool _initialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<DatabaseOptions>(o => { o.MigrateOnStartup = false; o.SeedOnStartup = false; });
            services.PostConfigure<PasswordHashingOptions>(o => o.BcryptWorkFactor = 4);
            services.PostConfigure<JwtOptions>(o => { o.PrivateKeyPem = TestKeys.PrivateKeyPem; o.PublicKeyPem = null; });

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

    /// <summary>Creates the schema and seed data once per factory.</summary>
    public void EnsureDatabase()
    {
        lock (_initLock)
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
    }

    /// <summary>Mints a token with the API's own issuer (same code path as the token endpoint).</summary>
    public string CreateToken(string role, string username, string? studentNo = null)
    {
        var issuer = Services.GetRequiredService<JwtTokenIssuer>();
        return issuer.Issue(new User { Username = username, Role = role, StudentNo = studentNo, PasswordHash = "-" });
    }

    public string StudentToken(string studentNo = "20230001") => CreateToken(Roles.Student, studentNo, studentNo);

    public string BankToken() => CreateToken(Roles.Bank, "bank-app");

    public string AdminToken() => CreateToken(Roles.Admin, "admin");

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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
