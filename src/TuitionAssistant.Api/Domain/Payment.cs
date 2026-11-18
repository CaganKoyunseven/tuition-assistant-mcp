namespace TuitionAssistant.Api.Domain;

/// <summary>A simulated tuition payment. No card data is ever stored.</summary>
public class Payment
{
    public Guid Id { get; set; }

    /// <summary>Caller-supplied idempotency key; globally unique.</summary>
    public required string TransactionId { get; set; }

    public Guid TuitionRecordId { get; set; }

    /// <summary>Denormalized from the tuition record for auditing.</summary>
    public required string StudentNo { get; set; }

    public required string Term { get; set; }

    public long AmountCents { get; set; }

    public string Currency { get; set; } = "USD";

    /// <summary>One of <see cref="PaymentChannels"/>.</summary>
    public required string Channel { get; set; }

    /// <summary>JWT subject of the caller that recorded the payment.</summary>
    public required string PerformedBy { get; set; }

    public DateTime RecordedAt { get; set; }

    public TuitionRecord? TuitionRecord { get; set; }
}
