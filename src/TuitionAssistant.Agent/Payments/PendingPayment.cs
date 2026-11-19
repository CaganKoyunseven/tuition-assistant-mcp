using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;

namespace TuitionAssistant.Agent.Payments;

public enum PendingState
{
    Pending,
    Used,
    Cancelled,
}

/// <summary>A payment the student has been asked to confirm. Nothing is paid until the Confirm endpoint is called.</summary>
public sealed class PendingPayment
{
    public required Guid PendingId { get; init; }

    public required string SessionId { get; init; }

    public required string StudentNo { get; init; }

    public required string Term { get; init; }

    public required decimal Amount { get; init; }

    public required decimal BalanceAtPreparation { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public PendingState State { get; set; } = PendingState.Pending;

    /// <summary>The summary shown to the model and to the UI.</summary>
    public object ToSummary() => new
    {
        pendingId = PendingId,
        studentNo = StudentNo,
        term = Term,
        amount = Amount,
        currentBalance = BalanceAtPreparation,
        remainingAfterPayment = BalanceAtPreparation - Amount,
        currency = "USD",
        expiresAt = ExpiresAt,
        status = "AWAITING_USER_CONFIRMATION",
    };
}

/// <summary>Holds pending payments in memory until they expire (documented limitation: one agent replica).</summary>
public sealed class PendingPaymentStore(IMemoryCache cache, IOptions<AgentOptions> options, TimeProvider clock)
{
    public object SyncRoot { get; } = new();

    public PendingPayment Create(string sessionId, string studentNo, string term, decimal amount, decimal balance)
    {
        var now = clock.GetUtcNow();
        var ttl = TimeSpan.FromSeconds(options.Value.PendingPaymentTtlSeconds);
        var pending = new PendingPayment
        {
            PendingId = Guid.NewGuid(),
            SessionId = sessionId,
            StudentNo = studentNo,
            Term = term,
            Amount = amount,
            BalanceAtPreparation = balance,
            CreatedAt = now,
            ExpiresAt = now + ttl,
        };
        // Keep the entry a little longer than its TTL so "expired" can be told apart from "never existed".
        cache.Set(pending.PendingId, pending, ttl + TimeSpan.FromMinutes(10));
        return pending;
    }

    public PendingPayment? Find(Guid id) => cache.TryGetValue(id, out PendingPayment? pending) ? pending : null;
}
