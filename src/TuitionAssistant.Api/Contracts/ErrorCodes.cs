namespace TuitionAssistant.Api.Contracts;

/// <summary>
/// Machine-readable error codes returned in the <c>code</c> extension of every ProblemDetails response.
/// The full catalog is documented in the README.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string InvalidStudentNo = "INVALID_STUDENT_NO";
    public const string InvalidTerm = "INVALID_TERM";
    public const string InvalidAmount = "INVALID_AMOUNT";
    public const string InvalidTransactionId = "INVALID_TRANSACTION_ID";
    public const string UnsupportedCurrency = "UNSUPPORTED_CURRENCY";
    public const string InvalidCsv = "INVALID_CSV";

    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    public const string Forbidden = "FORBIDDEN";
    public const string ForbiddenNotOwner = "FORBIDDEN_NOT_OWNER";

    public const string NotFound = "NOT_FOUND";
    public const string StudentNotFound = "STUDENT_NOT_FOUND";
    public const string TuitionNotFound = "TUITION_NOT_FOUND";

    public const string DuplicateTransaction = "DUPLICATE_TRANSACTION";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";

    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";

    public const string AmountExceedsBalance = "AMOUNT_EXCEEDS_BALANCE";
    public const string AlreadyPaid = "ALREADY_PAID";
    public const string AmountBelowPaid = "AMOUNT_BELOW_PAID";

    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>Default code used when an error response has no more specific code.</summary>
    public static string ForStatus(int status) => status switch
    {
        400 => ValidationError,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        413 => PayloadTooLarge,
        429 => RateLimitExceeded,
        >= 500 => InternalError,
        _ => $"HTTP_{status}",
    };
}
