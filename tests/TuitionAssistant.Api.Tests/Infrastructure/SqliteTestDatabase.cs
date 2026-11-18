using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Tests.Infrastructure;

/// <summary>
/// In-memory SQLite database for fast service-level tests.
/// SQLite is a TEST DOUBLE ONLY: every running environment uses PostgreSQL.
/// The in-memory database lives as long as the connection stays open.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public TuitionDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TuitionDbContext>().UseSqlite(_connection).Options);

    /// <summary>Low BCrypt cost keeps tests fast; production uses 11.</summary>
    public static IPasswordHasher FastPasswordHasher { get; } =
        new BcryptPasswordHasher(Options.Create(new PasswordHashingOptions { BcryptWorkFactor = 4 }));

    public async Task SeedAsync()
    {
        await using var db = CreateContext();
        await new DbSeeder(db, FastPasswordHasher, Options.Create(new SeedOptions()), TimeProvider.System).SeedAsync();
    }

    public void Dispose() => _connection.Dispose();
}
