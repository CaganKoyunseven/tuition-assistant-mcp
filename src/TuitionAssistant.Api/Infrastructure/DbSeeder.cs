using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Infrastructure;

/// <summary>
/// Creates the deterministic demo data documented in the README ("Test data").
/// Idempotent: every row is inserted only if its natural key does not exist yet,
/// so it is safe to run on every startup.
/// </summary>
public class DbSeeder(
    TuitionDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> options,
    TimeProvider clock)
{
    public const string DemoTerm = "2026-FALL";
    public const int PagingStudentCount = 25;

    // Fixed timestamp for seed payments so the data is identical on every run.
    private static readonly DateTime SeedPaymentTime = new(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc);

    private sealed record SeedRecord(string StudentNo, string Term, long AmountCents, params (string TransactionId, long Cents)[] Payments);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        await SeedStudentsAsync(now, cancellationToken);
        await SeedTuitionAsync(now, cancellationToken);
        await SeedUsersAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<(string StudentNo, string FullName)> Students()
    {
        yield return ("20230001", "Elif Demir");
        yield return ("20230002", "Mert Kaya");
        yield return ("20230003", "Zeynep Aydin");
        yield return ("20230004", "Can Yilmaz");
        for (var i = 1; i <= PagingStudentCount; i++)
        {
            yield return ($"{20239000 + i}", $"Paging Student {i:D2}");
        }
    }

    private static IEnumerable<SeedRecord> TuitionRecords()
    {
        // Main demo student: 6,000.00 assessed, 3,000.00 paid → balance 3,000.00.
        yield return new SeedRecord("20230001", DemoTerm, 600_000, ("SEED-TXN-0001", 150_000), ("SEED-TXN-0002", 150_000));
        // Overall vs. term balance: one term fully paid, one unpaid.
        yield return new SeedRecord("20230002", DemoTerm, 450_000, ("SEED-TXN-0003", 450_000));
        yield return new SeedRecord("20230002", "2027-SPRING", 450_000);
        // Balance 250.00: below the $300 installment threshold (Policy §3.1).
        yield return new SeedRecord("20230003", DemoTerm, 600_000, ("SEED-TXN-0004", 575_000));
        // Unpaid; the agent must not infer a scholarship (Policy §5.2).
        yield return new SeedRecord("20230004", DemoTerm, 450_000);
        // Paging students for the unpaid report: 1,000.00 + i × 137.00, nothing paid.
        for (var i = 1; i <= PagingStudentCount; i++)
        {
            yield return new SeedRecord($"{20239000 + i}", DemoTerm, 100_000 + i * 13_700);
        }
    }

    private async Task SeedStudentsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var existing = await db.Students.Select(s => s.StudentNo).ToHashSetAsync(cancellationToken);
        foreach (var (studentNo, fullName) in Students().Where(s => !existing.Contains(s.StudentNo)))
        {
            db.Students.Add(new Student { StudentNo = studentNo, FullName = fullName, CreatedAt = now });
        }
    }

    private async Task SeedTuitionAsync(DateTime now, CancellationToken cancellationToken)
    {
        var existing = (await db.TuitionRecords.Select(r => new { r.StudentNo, r.Term }).ToListAsync(cancellationToken))
            .Select(r => (r.StudentNo, r.Term))
            .ToHashSet();

        foreach (var seed in TuitionRecords().Where(s => !existing.Contains((s.StudentNo, s.Term))))
        {
            var record = new TuitionRecord
            {
                Id = Guid.NewGuid(),
                StudentNo = seed.StudentNo,
                Term = seed.Term,
                AmountCents = seed.AmountCents,
                PaidCents = seed.Payments.Sum(p => p.Cents),
                Currency = Money.Currency,
                CreatedAt = now,
                UpdatedAt = now,
            };

            foreach (var (transactionId, cents) in seed.Payments)
            {
                record.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transactionId,
                    StudentNo = seed.StudentNo,
                    Term = seed.Term,
                    AmountCents = cents,
                    Currency = Money.Currency,
                    Channel = PaymentChannels.BankApp,
                    PerformedBy = "seed",
                    RecordedAt = SeedPaymentTime,
                });
            }

            db.TuitionRecords.Add(record);
        }
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Users.Select(u => u.Username).ToHashSetAsync(cancellationToken);
        var settings = options.Value;

        void AddUser(string username, string secret, string role, string? studentNo = null)
        {
            if (existing.Contains(username))
            {
                return;
            }

            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = passwordHasher.Hash(secret),
                Role = role,
                StudentNo = studentNo,
            });
        }

        AddUser("admin", settings.AdminPassword, Roles.Admin);
        AddUser("bank-app", settings.BankClientSecret, Roles.Bank);
        foreach (var studentNo in new[] { "20230001", "20230002", "20230003", "20230004" })
        {
            AddUser(studentNo, settings.StudentPassword, Roles.Student, studentNo);
        }
    }
}
