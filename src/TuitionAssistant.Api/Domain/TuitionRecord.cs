namespace TuitionAssistant.Api.Domain;

/// <summary>Assessed tuition for one student and one term. Unique per (StudentNo, Term).</summary>
public class TuitionRecord
{
    public Guid Id { get; set; }

    public required string StudentNo { get; set; }

    /// <summary>Canonical term string, e.g. <c>2026-FALL</c>.</summary>
    public required string Term { get; set; }

    public long AmountCents { get; set; }

    public long PaidCents { get; set; }

    public string Currency { get; set; } = "USD";

    /// <summary>Optimistic concurrency token; rotated on every change to prevent double-spending races.</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Student? Student { get; set; }

    public List<Payment> Payments { get; set; } = [];

    /// <summary>Outstanding amount; never negative because PaidCents ≤ AmountCents is enforced.</summary>
    public long BalanceCents => AmountCents - PaidCents;

    public string Status => TuitionStatuses.From(AmountCents, PaidCents);
}
