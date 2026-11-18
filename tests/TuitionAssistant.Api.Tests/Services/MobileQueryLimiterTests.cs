using Microsoft.Extensions.Time.Testing;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Services;

public class MobileQueryLimiterTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 23, 59, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    private async Task<int> RegisterAsync(string studentNo)
    {
        await using var db = _database.CreateContext();
        return await new MobileQueryLimiter(db, _clock).RegisterQueryAsync(studentNo, CancellationToken.None);
    }

    [Fact]
    public async Task Register_ThreeCalls_ReturnsRemainingCountdown()
    {
        Assert.Equal(2, await RegisterAsync("20230001"));
        Assert.Equal(1, await RegisterAsync("20230001"));
        Assert.Equal(0, await RegisterAsync("20230001"));
    }

    [Fact]
    public async Task Register_FourthCall_Throws429WithRetryAfterUntilUtcMidnight()
    {
        for (var i = 0; i < 3; i++)
        {
            await RegisterAsync("20230001");
        }

        var ex = await Assert.ThrowsAsync<ApiException>(() => RegisterAsync("20230001"));
        Assert.Equal(429, ex.Status);
        Assert.Equal(ErrorCodes.RateLimitExceeded, ex.Code);
        Assert.Equal("60", ex.Headers["Retry-After"]); // clock is 23:59 UTC
    }

    [Fact]
    public async Task Register_CounterResetsOnNextUtcDay()
    {
        for (var i = 0; i < 3; i++)
        {
            await RegisterAsync("20230001");
        }

        _clock.Advance(TimeSpan.FromMinutes(2)); // now 00:01 UTC on the next day

        Assert.Equal(2, await RegisterAsync("20230001"));
    }

    [Fact]
    public async Task Register_CountsUnknownStudentNumbersToo()
    {
        Assert.Equal(2, await RegisterAsync("29999999"));
    }
}
