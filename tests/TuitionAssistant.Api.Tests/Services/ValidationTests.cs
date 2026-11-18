using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Errors;

namespace TuitionAssistant.Api.Tests.Services;

public class ValidationTests
{
    [Fact]
    public void Amount_ValidValue_ReturnsCents() => Assert.Equal(100_050, Validation.Amount(1000.50m));

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("1000000.01")]
    public void Amount_InvalidValue_ThrowsInvalidAmount(string? value)
    {
        decimal? amount = value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(ErrorCodes.InvalidAmount, Assert.Throws<ApiException>(() => Validation.Amount(amount)).Code);
    }

    [Fact]
    public void Amount_MaximumValue_IsAccepted() => Assert.Equal(100_000_000, Validation.Amount(1_000_000.00m));

    [Theory]
    [InlineData("BANK-2026-000123")]
    [InlineData("AGT_abc")]
    public void TransactionId_Valid_IsAccepted(string id) => Assert.Equal(id, Validation.TransactionId(id));

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public void TransactionId_Invalid_Throws(string id) =>
        Assert.Equal(ErrorCodes.InvalidTransactionId, Assert.Throws<ApiException>(() => Validation.TransactionId(id)).Code);

    [Fact]
    public void TransactionId_LongerThan64_Throws() =>
        Assert.Throws<ApiException>(() => Validation.TransactionId(new string('a', 65)));

    [Fact]
    public void Currency_IsCaseInsensitive() => Assert.Equal("USD", Validation.Currency(" usd "));

    [Fact]
    public void Paging_Defaults_Are1And20() => Assert.Equal((1, 20), Validation.Paging(null, null));
}
