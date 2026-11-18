namespace TuitionAssistant.Api.Domain;

/// <summary>Audit row for one processed Add Tuition batch (CSV) upload.</summary>
public class BatchImport
{
    public Guid Id { get; set; }

    public required string UploadedBy { get; set; }

    public required string FileName { get; set; }

    public int TotalRows { get; set; }

    public int Succeeded { get; set; }

    public int Failed { get; set; }

    public int Created { get; set; }

    public int Updated { get; set; }

    public DateTime CreatedAt { get; set; }
}
