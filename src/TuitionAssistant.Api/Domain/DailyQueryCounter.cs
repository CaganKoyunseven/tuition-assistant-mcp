namespace TuitionAssistant.Api.Domain;

/// <summary>
/// Number of mobile tuition queries for one student number on one UTC day.
/// No foreign key: unknown student numbers are counted too.
/// </summary>
public class DailyQueryCounter
{
    public required string StudentNo { get; set; }

    public DateOnly DayUtc { get; set; }

    public int Count { get; set; }
}
