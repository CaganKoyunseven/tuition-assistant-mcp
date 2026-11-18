namespace TuitionAssistant.Api.Infrastructure;

/// <summary>
/// Demo credentials created by <see cref="DbSeeder"/> (configuration section <c>Seed</c>).
/// Override with environment variables <c>SEED__ADMINPASSWORD</c>, <c>SEED__STUDENTPASSWORD</c>
/// and <c>SEED__BANKCLIENTSECRET</c>.
/// </summary>
public class SeedOptions
{
    public string AdminPassword { get; set; } = "Admin#2026";

    public string StudentPassword { get; set; } = "Student#2026";

    public string BankClientSecret { get; set; } = "BankSecret#2026";
}

/// <summary>Database startup behaviour (configuration section <c>Database</c>).</summary>
public class DatabaseOptions
{
    /// <summary>Apply pending EF Core migrations at startup (PostgreSQL only).</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Run the idempotent demo-data seeder at startup.</summary>
    public bool SeedOnStartup { get; set; }
}
