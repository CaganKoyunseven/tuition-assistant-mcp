using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Tests.Domain;

public class TuitionStatusesTests
{
    [Theory]
    [InlineData(600_000L, 0L, TuitionStatuses.Unpaid)]
    [InlineData(600_000L, 300_000L, TuitionStatuses.PartiallyPaid)]
    [InlineData(600_000L, 600_000L, TuitionStatuses.Paid)]
    public void From_PaidAmount_ReturnsExpectedStatus(long amount, long paid, string expected)
    {
        Assert.Equal(expected, TuitionStatuses.From(amount, paid));
    }

    [Fact]
    public void TuitionRecord_BalanceIsTotalMinusPaid()
    {
        var record = new TuitionRecord { StudentNo = "20230001", Term = "2026-FALL", AmountCents = 600_000, PaidCents = 250_000 };

        Assert.Equal(350_000, record.BalanceCents);
        Assert.Equal(TuitionStatuses.PartiallyPaid, record.Status);
    }
}
