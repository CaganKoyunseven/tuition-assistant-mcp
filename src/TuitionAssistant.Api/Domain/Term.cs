using System.Text.RegularExpressions;

namespace TuitionAssistant.Api.Domain;

/// <summary>
/// An academic billing period such as <c>2026-FALL</c> or <c>2027-SPRING</c>.
/// Input is case-insensitive; the canonical form is upper case.
/// </summary>
public readonly partial record struct Term(int Year, TermSeason Season) : IComparable<Term>
{
    [GeneratedRegex(@"^(\d{4})-(FALL|SPRING|SUMMER)$")]
    private static partial Regex Pattern();

    /// <summary>Parses a term string. Returns false when the format is not <c>YYYY-FALL|SPRING|SUMMER</c>.</summary>
    public static bool TryParse(string? input, out Term term)
    {
        term = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var match = Pattern().Match(input.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            return false;
        }

        var season = match.Groups[2].Value switch
        {
            "SPRING" => TermSeason.Spring,
            "SUMMER" => TermSeason.Summer,
            _ => TermSeason.Fall,
        };
        term = new Term(int.Parse(match.Groups[1].Value), season);
        return true;
    }

    /// <summary>Chronological order: year first, then SPRING &lt; SUMMER &lt; FALL.</summary>
    public int CompareTo(Term other)
    {
        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Season.CompareTo(other.Season);
    }

    /// <summary>Canonical form, e.g. <c>2026-FALL</c>.</summary>
    public override string ToString() => $"{Year:D4}-{Season.ToString().ToUpperInvariant()}";
}
