using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure;

/// <summary>
/// EF Core context for the tuition database. PostgreSQL in every running environment;
/// SQLite in-memory is used only as a test double inside the unit tests.
/// The model avoids provider-specific SQL so both providers can create it.
/// </summary>
public class TuitionDbContext(DbContextOptions<TuitionDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<TuitionRecord> TuitionRecords => Set<TuitionRecord>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BatchImport> BatchImports => Set<BatchImport>();
    public DbSet<DailyQueryCounter> DailyQueryCounters => Set<DailyQueryCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TuitionDbContext).Assembly);

        // Database naming convention: snake_case columns (tables are named in each configuration).
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are UTC (timestamptz in PostgreSQL).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }

    /// <summary>Stores DateTime values as UTC and always reads them back with <see cref="DateTimeKind.Utc"/>.</summary>
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
