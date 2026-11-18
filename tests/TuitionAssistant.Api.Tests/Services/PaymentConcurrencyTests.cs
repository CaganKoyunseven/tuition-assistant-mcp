using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Security;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Services;

/// <summary>
/// Simulates the race deterministically: just before payment A is saved, an interceptor commits a
/// competing payment B on the same record. A must not overdraw the balance.
/// </summary>
public class PaymentConcurrencyTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();

    public Task InitializeAsync() => _database.SeedAsync();

    public Task DisposeAsync()
    {
        _database.Dispose();
        return Task.CompletedTask;
    }

    private static ClaimsPrincipal Bank() => new(new ClaimsIdentity(
        [new Claim(TuitionClaims.Subject, "bank-app"), new Claim(TuitionClaims.Role, Roles.Bank)], "test", TuitionClaims.Subject, TuitionClaims.Role));

    private static PayTuitionRequest Request(decimal amount, string transactionId) =>
        new() { StudentNo = "20230001", Term = "2026-FALL", Amount = amount, TransactionId = transactionId };

    [Fact]
    public async Task TwoSimultaneousPayments_CannotOverdrawBalance()
    {
        // Balance is 3,000. A wants 2,000; B (committed first, mid-flight) takes 2,000 as well.
        var competitor = new CompetingPaymentInterceptor(_database, Request(2000m, "T-RACE-B"));
        await using var db = _database.CreateContext(competitor);
        var service = new PaymentService(db, TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ApiException>(() => service.PayAsync(Request(2000m, "T-RACE-A"), Bank(), null, CancellationToken.None));

        Assert.Equal(ErrorCodes.AmountExceedsBalance, ex.Code); // retried with fresh data, then rejected
        await using var check = _database.CreateContext();
        var record = await check.TuitionRecords.SingleAsync(r => r.StudentNo == "20230001");
        Assert.Equal(500_000, record.PaidCents); // 3,000 seed + 2,000 from B only
        Assert.Equal(1_000_00, record.BalanceCents);
    }

    [Fact]
    public async Task ConcurrentPaymentThatStillFits_SucceedsAfterRetry()
    {
        var competitor = new CompetingPaymentInterceptor(_database, Request(1000m, "T-RACE-D"));
        await using var db = _database.CreateContext(competitor);
        var service = new PaymentService(db, TimeProvider.System);

        var result = await service.PayAsync(Request(1000m, "T-RACE-C"), Bank(), null, CancellationToken.None);

        Assert.Equal(1000.00m, result.RemainingBalance);
    }

    /// <summary>Commits one competing payment through a separate context the first time A saves.</summary>
    private sealed class CompetingPaymentInterceptor(SqliteTestDatabase database, PayTuitionRequest competing) : SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;
                await using var other = database.CreateContext();
                await new PaymentService(other, TimeProvider.System).PayAsync(competing, Bank(), null, cancellationToken);
            }

            return result;
        }
    }
}
