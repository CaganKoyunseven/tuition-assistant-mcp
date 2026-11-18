using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Tests.Infrastructure;

public class TuitionApiFactoryTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    [Fact]
    public async Task Factory_ReplacesPostgresWithSeededSqlite()
    {
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TuitionDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
        Assert.Equal(6, await db.Users.CountAsync());
    }
}
