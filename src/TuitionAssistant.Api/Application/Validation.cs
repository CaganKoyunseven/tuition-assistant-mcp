using System.Text.RegularExpressions;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;

namespace TuitionAssistant.Api.Application;

/// <summary>
/// Input format rules shared by all operations. Each rule throws an <see cref="ApiException"/>
/// (400) with a specific error code so clients can tell exactly which field is wrong.
/// </summary>
public static partial class Validation
{
    public const decimal MaxAmount = 1_000_000.00m;
    public const int MaxPageSize = 100;

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex StudentNoPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex TransactionIdPattern();

    /// <summary>Student numbers have exactly 8 digits, e.g. <c>20230001</c>.</summary>
    public static string StudentNo(string? value)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || !StudentNoPattern().IsMatch(trimmed))
        {
            throw ApiException.Validation("Student number must be exactly 8 digits (e.g. 20230001).", ErrorCodes.InvalidStudentNo);
        }

        return trimmed;
    }

    /// <summary>Returns the canonical term (<c>2026-FALL</c>); input is case-insensitive.</summary>
    public static string Term(string? value)
    {
        if (!Domain.Term.TryParse(value, out var term))
        {
            throw ApiException.Validation("Term must look like YYYY-FALL, YYYY-SPRING or YYYY-SUMMER (e.g. 2026-FALL).", ErrorCodes.InvalidTerm);
        }

        return term.ToString();
    }

    /// <summary>Optional term: null or empty means "overall".</summary>
    public static string? OptionalTerm(string? value) => string.IsNullOrWhiteSpace(value) ? null : Term(value);

    /// <summary>Amount must be &gt; 0, ≤ 1,000,000.00 and have at most two decimals (no rounding). Returns cents.</summary>
    public static long Amount(decimal? value)
    {
        if (value is not { } amount || amount <= 0 || amount > MaxAmount || !Money.TryToCents(amount, out var cents))
        {
            throw ApiException.Validation(
                $"Amount must be greater than 0, at most {MaxAmount:0.00} and have at most two decimal places.", ErrorCodes.InvalidAmount);
        }

        return cents;
    }

    /// <summary>Only USD is supported (case-insensitive). Returns the upper-case code.</summary>
    public static string Currency(string? value)
    {
        var upper = value?.Trim().ToUpperInvariant();
        if (upper != Money.Currency)
        {
            throw ApiException.Validation($"Currency '{value}' is not supported. Only USD is accepted.", ErrorCodes.UnsupportedCurrency);
        }

        return upper;
    }

    /// <summary>Transaction IDs: 1–64 characters, letters, digits, '-' and '_'.</summary>
    public static string TransactionId(string? value)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || !TransactionIdPattern().IsMatch(trimmed))
        {
            throw ApiException.Validation(
                "Transaction ID must be 1-64 characters of letters, digits, '-' or '_'.", ErrorCodes.InvalidTransactionId);
        }

        return trimmed;
    }

    public static (int Page, int PageSize) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var size = pageSize ?? 20;
        if (p < 1)
        {
            throw ApiException.Validation("page must be 1 or greater.");
        }

        if (size is < 1 or > MaxPageSize)
        {
            throw ApiException.Validation($"pageSize must be between 1 and {MaxPageSize}.");
        }

        return (p, size);
    }
}
