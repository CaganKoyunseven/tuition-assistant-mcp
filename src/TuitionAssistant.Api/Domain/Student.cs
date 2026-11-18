namespace TuitionAssistant.Api.Domain;

/// <summary>A student identified by an 8-digit student number.</summary>
public class Student
{
    public required string StudentNo { get; set; }

    /// <summary>Null for students auto-created by Add Tuition (the API has no "create student" operation).</summary>
    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<TuitionRecord> TuitionRecords { get; set; } = [];
}
