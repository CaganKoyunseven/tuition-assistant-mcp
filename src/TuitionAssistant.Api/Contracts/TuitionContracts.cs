namespace TuitionAssistant.Api.Contracts;

/// <summary>Tuition totals for one term.</summary>
public class TermTuitionDto
{
    /// <example>2026-FALL</example>
    public required string Term { get; init; }

    /// <summary>Assessed tuition for the term.</summary>
    /// <example>6000.00</example>
    public decimal TuitionTotal { get; init; }

    /// <example>3000.00</example>
    public decimal PaidAmount { get; init; }

    /// <summary>Outstanding balance = tuition total − paid amount.</summary>
    /// <example>3000.00</example>
    public decimal Balance { get; init; }

    /// <summary><c>UNPAID</c>, <c>PARTIALLY_PAID</c> or <c>PAID</c>.</summary>
    /// <example>PARTIALLY_PAID</example>
    public required string Status { get; init; }
}

/// <summary>
/// Query Tuition result. With a <c>term</c> parameter the scope is <c>TERM</c> and the totals are for that term;
/// without it the scope is <c>OVERALL</c> and the totals are summed over all terms (<c>terms</c> lists each term).
/// </summary>
public class TuitionQueryResponse
{
    /// <example>20230001</example>
    public required string StudentNo { get; init; }

    /// <summary><c>TERM</c> or <c>OVERALL</c>.</summary>
    /// <example>TERM</example>
    public required string Scope { get; init; }

    /// <summary>The requested term, or null for the overall scope.</summary>
    /// <example>2026-FALL</example>
    public string? Term { get; init; }

    /// <example>USD</example>
    public string Currency { get; init; } = "USD";

    /// <example>6000.00</example>
    public decimal TuitionTotal { get; init; }

    /// <example>3000.00</example>
    public decimal PaidAmount { get; init; }

    /// <example>3000.00</example>
    public decimal Balance { get; init; }

    /// <example>PARTIALLY_PAID</example>
    public required string Status { get; init; }

    /// <summary>Per-term breakdown in chronological order (SPRING &lt; SUMMER &lt; FALL within a year).</summary>
    public required IReadOnlyList<TermTuitionDto> Terms { get; init; }
}
