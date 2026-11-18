using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Database;

public class DbSeederTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Seed_EmptyDatabase_CreatesDocumentedDemoData()
    {
        await _database.SeedAsync();

        await using var db = _database.CreateContext();
        Assert.Equal(29, await db.Students.CountAsync());
        Assert.Equal(30, await db.TuitionRecords.CountAsync());
        Assert.Equal(4, await db.Payments.CountAsync());
        Assert.Equal(6, await db.Users.CountAsync());

        var elif = await db.TuitionRecords.SingleAsync(r => r.StudentNo == "20230001" && r.Term == "2026-FALL");
        Assert.Equal(600_000, elif.AmountCents);
        Assert.Equal(300_000, elif.BalanceCents);

        var zeynep = await db.TuitionRecords.SingleAsync(r => r.StudentNo == "20230003");
        Assert.Equal(25_000, zeynep.BalanceCents);

        var paging25 = await db.TuitionRecords.SingleAsync(r => r.StudentNo == "20239025");
        Assert.Equal(100_000 + 25 * 13_700, paging25.AmountCents);
    }

    [Fact]
    public async Task Seed_RunTwice_IsIdempotent()
    {
        await _database.SeedAsync();
        await _database.SeedAsync();

        await using var db = _database.CreateContext();
        Assert.Equal(29, await db.Students.CountAsync());
        Assert.Equal(30, await db.TuitionRecords.CountAsync());
        Assert.Equal(4, await db.Payments.CountAsync());
        Assert.Equal(6, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Seed_PaidAmountsEqualSumOfSeedPayments()
    {
        await _database.SeedAsync();

        await using var db = _database.CreateContext();
        var records = await db.TuitionRecords.Include(r => r.Payments).ToListAsync();
        Assert.All(records, r => Assert.Equal(r.Payments.Sum(p => p.AmountCents), r.PaidCents));
    }

    [Fact]
    public async Task Seed_UsersHaveRolesAndHashedPasswords()
    {
        await _database.SeedAsync();

        await using var db = _database.CreateContext();
        var student = await db.Users.SingleAsync(u => u.Username == "20230001");
        Assert.Equal(Roles.Student, student.Role);
        Assert.Equal("20230001", student.StudentNo);
        Assert.NotEqual("Student#2026", student.PasswordHash);
        Assert.True(SqliteTestDatabase.FastPasswordHasher.Verify("Student#2026", student.PasswordHash));

        Assert.Equal(Roles.Bank, (await db.Users.SingleAsync(u => u.Username == "bank-app")).Role);
        Assert.Equal(Roles.Admin, (await db.Users.SingleAsync(u => u.Username == "admin")).Role);
    }
}
