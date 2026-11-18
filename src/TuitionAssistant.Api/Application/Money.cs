namespace TuitionAssistant.Api.Application;

/// <summary>
/// Money helpers. Amounts are stored as integer cents (<c>bigint</c>) so arithmetic is exact
/// and sorting works in every database provider. The API exposes decimals with two places.
/// </summary>
public static class Money
{
    /// <summary>The only supported currency (the policy document uses US dollars).</summary>
    public const string Currency = "USD";

    /// <summary>
    /// Converts a decimal amount to cents. Returns false if the amount has more than two
    /// decimal places (no rounding is ever performed) or does not fit in a 64-bit integer.
    /// </summary>
    public static bool TryToCents(decimal amount, out long cents)
    {
        cents = 0;
        decimal scaled;
        try
        {
            scaled = amount * 100m;
        }
        catch (OverflowException)
        {
            return false;
        }

        if (scaled != decimal.Truncate(scaled) || scaled > long.MaxValue || scaled < long.MinValue)
        {
            return false;
        }

        cents = (long)scaled;
        return true;
    }

    /// <summary>Converts a decimal amount to cents or throws if it has more than two decimal places.</summary>
    public static long ToCents(decimal amount) =>
        TryToCents(amount, out var cents)
            ? cents
            : throw new ArgumentException($"Amount {amount} must have at most two decimal places.", nameof(amount));

    /// <summary>Converts cents to a decimal that always carries two decimal places (e.g. 300000 → 3000.00).</summary>
    public static decimal FromCents(long cents) => cents * 0.01m;
}
