using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Application;

/// <summary>
/// Second layer of the "3 mobile queries per student per day" rule. Kong enforces the limit first
/// (keyed by the student number in the path); this database counter survives gateway restarts.
/// The day boundary is the UTC calendar day, the same window Kong uses.
/// </summary>
public class MobileQueryLimiter(TuitionDbContext db, TimeProvider clock)
{
    public const int DailyLimit = 3;

    /// <summary>
    /// Counts one query for the student number and returns the remaining queries for today.
    /// Throws 429 when the limit is exceeded. Unknown student numbers are counted too.
    /// </summary>
    public async Task<int> RegisterQueryAsync(string studentNo, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        // Atomic upsert: concurrent requests cannot both read the same old value.
        // The same statement works on PostgreSQL and on SQLite (used by the unit tests).
        var counts = await db.Database.SqlQuery<int>($"""
            INSERT INTO daily_query_counters (student_no, day_utc, count)
            VALUES ({studentNo}, {today}, 1)
            ON CONFLICT (student_no, day_utc) DO UPDATE SET count = daily_query_counters.count + 1
            RETURNING count AS "Value"
            """).ToListAsync(cancellationToken);
        var count = counts.Single();

        if (count > DailyLimit)
        {
            var nextMidnight = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var retryAfter = (int)Math.Ceiling((nextMidnight - now).TotalSeconds);
            throw new ApiException(StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimitExceeded, "Rate limit exceeded",
                    $"Daily limit reached: max {DailyLimit} tuition queries per student per day.")
                .WithHeader("Retry-After", retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .WithHeader("X-Daily-Limit", DailyLimit.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .WithHeader("X-Daily-Remaining", "0");
        }

        return DailyLimit - count;
    }
}
