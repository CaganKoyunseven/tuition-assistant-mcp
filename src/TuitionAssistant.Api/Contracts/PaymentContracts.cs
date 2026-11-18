namespace TuitionAssistant.Api.Contracts;

/// <summary>Pay Tuition request. Payments are simulated: no card data is accepted or stored.</summary>
public class PayTuitionRequest
{
    /// <example>20230001</example>
    public string? StudentNo { get; set; }

    /// <example>2026-FALL</example>
    public string? Term { get; set; }

    /// <summary>Amount in USD; &gt; 0, at most two decimals, not more than the outstanding balance.</summary>
    /// <example>1000.00</example>
    public decimal? Amount { get; set; }

    /// <summary>Caller-generated unique ID (idempotency key). Re-using it returns 409.</summary>
    /// <example>BANK-2026-000123</example>
    public string? TransactionId { get; set; }
}

/// <summary>Result of a successful payment.</summary>
public class PaymentResponse
{
    /// <example>SUCCESS</example>
    public string Status { get; init; } = "SUCCESS";

    public Guid PaymentId { get; init; }

    /// <example>BANK-2026-000123</example>
    public required string TransactionId { get; init; }

    /// <example>20230001</example>
    public required string StudentNo { get; init; }

    /// <example>2026-FALL</example>
    public required string Term { get; init; }

    /// <example>USD</example>
    public string Currency { get; init; } = "USD";

    /// <summary>Amount of this payment.</summary>
    /// <example>1000.00</example>
    public decimal PaidAmount { get; init; }

    /// <summary>Total paid for the term after this payment.</summary>
    /// <example>4000.00</example>
    public decimal TotalPaid { get; init; }

    /// <example>2000.00</example>
    public decimal RemainingBalance { get; init; }

    /// <summary>Record status after the payment.</summary>
    /// <example>PARTIALLY_PAID</example>
    public required string RecordStatus { get; init; }

    /// <summary><c>BANK_APP</c>, <c>AI_AGENT</c> or <c>STUDENT</c>.</summary>
    /// <example>BANK_APP</example>
    public required string Channel { get; init; }

    public DateTime RecordedAt { get; init; }
}

/// <summary>The original payment returned with a 409 DUPLICATE_TRANSACTION error.</summary>
public class OriginalPaymentDto
{
    public Guid PaymentId { get; init; }

    public required string StudentNo { get; init; }

    public required string Term { get; init; }

    public decimal Amount { get; init; }

    public DateTime RecordedAt { get; init; }
}
