using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Database;

/// <summary>Verifies that the database itself enforces the key business invariants.</summary>
public class DatabaseConstraintTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();

    public Task InitializeAsync() => _database.SeedAsync();

    public Task DisposeAsync()
    {
        _database.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Payment_DuplicateTransactionId_IsRejectedByUniqueIndex()
    {
        await using var db = _database.CreateContext();
        var record = await db.TuitionRecords.SingleAsync(r => r.StudentNo == "20230004");
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), TransactionId = "SEED-TXN-0001", TuitionRecordId = record.Id,
            StudentNo = "20230004", Term = record.Term, AmountCents = 100, Channel = PaymentChannels.BankApp,
            PerformedBy = "test", RecordedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TuitionRecord_DuplicateStudentAndTerm_IsRejectedByUniqueIndex()
    {
        await using var db = _database.CreateContext();
        db.TuitionRecords.Add(new TuitionRecord
        {
            Id = Guid.NewGuid(), StudentNo = "20230001", Term = "2026-FALL", AmountCents = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TuitionRecord_PaidAboveTotal_IsRejectedByCheckConstraint()
    {
        await using var db = _database.CreateContext();
        var record = await db.TuitionRecords.SingleAsync(r => r.StudentNo == "20230001");
        record.PaidCents = record.AmountCents + 1;

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TuitionRecord_StaleConcurrencyStamp_ThrowsConcurrencyException()
    {
        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();
        var a = await first.TuitionRecords.SingleAsync(r => r.StudentNo == "20230001");
        var b = await second.TuitionRecords.SingleAsync(r => r.StudentNo == "20230001");

        a.PaidCents += 100;
        a.ConcurrencyStamp = Guid.NewGuid();
        await first.SaveChangesAsync();

        b.PaidCents += 100;
        b.ConcurrencyStamp = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Timestamps_AreReadBackAsUtc()
    {
        await using var db = _database.CreateContext();
        var payment = await db.Payments.FirstAsync();

        Assert.Equal(DateTimeKind.Utc, payment.RecordedAt.Kind);
    }
}
