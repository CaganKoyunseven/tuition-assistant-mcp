namespace TuitionAssistant.Api.Domain;

/// <summary>
/// Academic season. The numeric values define the chronological order inside a year
/// (SPRING &lt; SUMMER &lt; FALL).
/// </summary>
public enum TermSeason
{
    Spring = 1,
    Summer = 2,
    Fall = 3,
}
