using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Tests.Domain;

public class TermTests
{
    [Theory]
    [InlineData("2026-FALL", 2026, TermSeason.Fall)]
    [InlineData("2027-spring", 2027, TermSeason.Spring)]
    [InlineData("  2026-Summer ", 2026, TermSeason.Summer)]
    public void TryParse_ValidInput_IsParsedCaseInsensitively(string input, int year, TermSeason season)
    {
        Assert.True(Term.TryParse(input, out var term));
        Assert.Equal(new Term(year, season), term);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026")]
    [InlineData("2026-WINTER")]
    [InlineData("26-FALL")]
    [InlineData("2026_FALL")]
    [InlineData("2026-FALL-X")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        Assert.False(Term.TryParse(input, out _));
    }

    [Fact]
    public void ToString_ReturnsCanonicalUpperCaseForm()
    {
        Assert.True(Term.TryParse("2027-spring", out var term));
        Assert.Equal("2027-SPRING", term.ToString());
    }

    [Fact]
    public void CompareTo_OrdersByYearThenSpringSummerFall()
    {
        var terms = new[] { "2027-SPRING", "2026-FALL", "2026-SPRING", "2026-SUMMER" }
            .Select(t => { Term.TryParse(t, out var term); return term; })
            .Order()
            .Select(t => t.ToString());

        Assert.Equal(["2026-SPRING", "2026-SUMMER", "2026-FALL", "2027-SPRING"], terms);
    }
}
