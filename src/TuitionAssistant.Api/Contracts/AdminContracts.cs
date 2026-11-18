namespace TuitionAssistant.Api.Contracts;

/// <summary>Add Tuition request (create or update the assessed tuition for a student and term).</summary>
public class AddTuitionRequest
{
    /// <example>20230005</example>
    public string? StudentNo { get; set; }

    /// <example>2027-SPRING</example>
    public string? Term { get; set; }

    /// <summary>Assessed tuition total in USD (&gt; 0, at most two decimals).</summary>
    /// <example>6000.00</example>
    public decimal? Amount { get; set; }

    /// <summary>Only <c>USD</c> is supported.</summary>
    /// <example>USD</example>
    public string? Currency { get; set; }
}

/// <summary>Add Tuition result.</summary>
public class AddTuitionResponse
{
    /// <summary><c>CREATED</c> (201), <c>UPDATED</c> or <c>UNCHANGED</c> (200).</summary>
    /// <example>CREATED</example>
    public required string Status { get; init; }

    /// <example>20230005</example>
    public required string StudentNo { get; init; }

    /// <example>2027-SPRING</example>
    public required string Term { get; init; }

    /// <example>6000.00</example>
    public decimal TuitionTotal { get; init; }

    /// <example>0.00</example>
    public decimal PaidAmount { get; init; }

    /// <example>6000.00</example>
    public decimal Balance { get; init; }

    /// <example>USD</example>
    public string Currency { get; init; } = "USD";

    /// <summary>True when the student number was unknown and a student record was created.</summary>
    public bool StudentCreated { get; init; }
}

/// <summary>One failed row of a batch import.</summary>
public class BatchRowError
{
    /// <summary>Line number in the file (the header is line 1).</summary>
    /// <example>3</example>
    public int Line { get; init; }

    /// <example>2023X001</example>
    public string? StudentNo { get; init; }

    /// <example>2026-FALL</example>
    public string? Term { get; init; }

    /// <example>INVALID_STUDENT_NO</example>
    public required string Code { get; init; }

    public required string Message { get; init; }
}

/// <summary>Batch import summary. Rows are processed independently (partial success is possible).</summary>
public class BatchImportResponse
{
    public Guid BatchId { get; init; }

    /// <example>5</example>
    public int TotalRows { get; init; }

    /// <example>3</example>
    public int Succeeded { get; init; }

    /// <example>2</example>
    public int Failed { get; init; }

    /// <example>2</example>
    public int Created { get; init; }

    /// <example>1</example>
    public int Updated { get; init; }

    /// <example>0</example>
    public int Unchanged { get; init; }

    /// <example>1</example>
    public int StudentsCreated { get; init; }

    public required IReadOnlyList<BatchRowError> Errors { get; init; }
}

/// <summary>One student with an outstanding balance.</summary>
public class UnpaidTuitionItem
{
    /// <example>20230004</example>
    public required string StudentNo { get; init; }

    /// <example>Can Yilmaz</example>
    public string? FullName { get; init; }

    /// <example>4500.00</example>
    public decimal TuitionTotal { get; init; }

    /// <example>0.00</example>
    public decimal PaidAmount { get; init; }

    /// <example>4500.00</example>
    public decimal Balance { get; init; }

    /// <example>UNPAID</example>
    public required string Status { get; init; }
}

/// <summary>Paged list of students with unpaid tuition for a term, ordered by balance (highest first).</summary>
public class UnpaidTuitionResponse
{
    /// <example>2026-FALL</example>
    public required string Term { get; init; }

    /// <example>1</example>
    public int Page { get; init; }

    /// <example>20</example>
    public int PageSize { get; init; }

    /// <example>28</example>
    public int TotalCount { get; init; }

    /// <example>2</example>
    public int TotalPages { get; init; }

    /// <summary>Sum of all outstanding balances for the term (all pages).</summary>
    public decimal TotalOutstanding { get; init; }

    /// <example>USD</example>
    public string Currency { get; init; } = "USD";

    public required IReadOnlyList<UnpaidTuitionItem> Items { get; init; }
}
