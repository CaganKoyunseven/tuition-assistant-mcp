using TuitionAssistant.Api.Application;

namespace TuitionAssistant.Api.Tests.Domain;

public class MoneyTests
{
    [Theory]
    [InlineData("3000", 300_000)]
    [InlineData("3000.5", 300_050)]
    [InlineData("0.01", 1)]
    [InlineData("1000000.00", 100_000_000)]
    public void ToCents_ValidAmount_ReturnsExactCents(string amount, long expected)
    {
        Assert.Equal(expected, Money.ToCents(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("10.001")]
    [InlineData("0.005")]
    public void ToCents_MoreThanTwoDecimals_IsRejectedWithoutRounding(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(Money.TryToCents(value, out _));
        Assert.Throws<ArgumentException>(() => Money.ToCents(value));
    }

    [Fact]
    public void TryToCents_HugeAmount_ReturnsFalseInsteadOfOverflowing()
    {
        Assert.False(Money.TryToCents(decimal.MaxValue, out _));
    }

    [Fact]
    public void FromCents_AlwaysHasTwoDecimalPlaces()
    {
        Assert.Equal("3000.00", Money.FromCents(300_000).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("0.05", Money.FromCents(5).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(123_456_789L)]
    public void FromCents_ThenToCents_RoundTrips(long cents)
    {
        Assert.Equal(cents, Money.ToCents(Money.FromCents(cents)));
    }
}
