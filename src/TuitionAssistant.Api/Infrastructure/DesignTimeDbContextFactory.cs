using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TuitionAssistant.Api.Infrastructure;

/// <summary>
/// Used only by the <c>dotnet ef</c> tool to generate migrations. It never connects:
/// the connection string is a placeholder that only selects the PostgreSQL provider.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TuitionDbContext>
{
    public TuitionDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TuitionDbContext>()
            .UseNpgsql("Host=localhost;Database=tuition_design_time")
            .Options);
}
