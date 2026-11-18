using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Application;

/// <summary>
/// Add Tuition – Batch. Reads a CSV with the header <c>studentNo,term,amount,currency</c> and applies
/// the single Add Tuition rules to every row independently (partial success). Rows are processed in
/// file order, so a later row for the same (studentNo, term) updates the earlier one.
/// </summary>
public class TuitionImportService(TuitionAdminService adminService, TuitionDbContext db, TimeProvider clock)
{
    public const long MaxFileBytes = 1_048_576;
    public const int MaxRows = 5_000;
    private static readonly string[] ExpectedHeader = ["studentno", "term", "amount", "currency"];

    public async Task<BatchImportResponse> ImportAsync(Stream csv, string fileName, string uploadedBy, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(csv);

        int created = 0, updated = 0, unchanged = 0, studentsCreated = 0;
        var errors = new List<BatchRowError>();

        foreach (var (line, fields) in rows)
        {
            var studentNo = Field(fields, 0);
            var term = Field(fields, 1);
            try
            {
                if (fields.Length != ExpectedHeader.Length)
                {
                    throw ApiException.Validation($"Expected {ExpectedHeader.Length} columns but found {fields.Length}.");
                }

                if (!decimal.TryParse(Field(fields, 2), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
                {
                    throw ApiException.Validation($"Amount '{Field(fields, 2)}' is not a valid number (use '.' as decimal separator).", ErrorCodes.InvalidAmount);
                }

                var result = await adminService.AddOrUpdateAsync(
                    new AddTuitionRequest { StudentNo = studentNo, Term = term, Amount = amount, Currency = Field(fields, 3) },
                    cancellationToken);

                switch (result.Status)
                {
                    case TuitionAdminService.Created: created++; break;
                    case TuitionAdminService.Updated: updated++; break;
                    default: unchanged++; break;
                }

                if (result.StudentCreated)
                {
                    studentsCreated++;
                }
            }
            catch (ApiException ex)
            {
                errors.Add(new BatchRowError { Line = line, StudentNo = studentNo, Term = term, Code = ex.Code, Message = ex.Message });
            }
        }

        var batch = new BatchImport
        {
            Id = Guid.NewGuid(),
            UploadedBy = uploadedBy,
            FileName = Truncate(fileName, 200),
            TotalRows = rows.Count,
            Succeeded = rows.Count - errors.Count,
            Failed = errors.Count,
            Created = created,
            Updated = updated,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.BatchImports.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        return new BatchImportResponse
        {
            BatchId = batch.Id,
            TotalRows = batch.TotalRows,
            Succeeded = batch.Succeeded,
            Failed = batch.Failed,
            Created = created,
            Updated = updated,
            Unchanged = unchanged,
            StudentsCreated = studentsCreated,
            Errors = errors,
        };
    }

    /// <summary>Parses the file and checks the header; returns (line number, fields) per data row.</summary>
    private static async Task<List<(int Line, string[] Fields)>> ReadRowsAsync(Stream csv)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { TrimOptions = TrimOptions.Trim };
        using var reader = new StreamReader(csv);
        using var parser = new CsvParser(reader, config);
        var rows = new List<(int, string[])>();

        try
        {
            if (!await parser.ReadAsync() || parser.Record is null)
            {
                throw InvalidCsv("The file is empty.");
            }

            var header = parser.Record.Select(h => h.Trim().ToLowerInvariant()).ToArray();
            if (!header.SequenceEqual(ExpectedHeader))
            {
                throw InvalidCsv("The header row must be exactly: studentNo,term,amount,currency");
            }

            while (await parser.ReadAsync())
            {
                var fields = parser.Record ?? [];
                if (fields.All(string.IsNullOrWhiteSpace))
                {
                    continue; // ignore blank lines
                }

                if (rows.Count == MaxRows)
                {
                    throw InvalidCsv($"The file has more than {MaxRows} data rows.");
                }

                rows.Add((parser.Row, fields));
            }
        }
        catch (CsvHelperException ex)
        {
            throw InvalidCsv($"The file could not be parsed as CSV: {ex.GetType().Name}.");
        }

        if (rows.Count == 0)
        {
            throw InvalidCsv("The file contains a header but no data rows.");
        }

        return rows;
    }

    private static ApiException InvalidCsv(string detail) => ApiException.Validation(detail, ErrorCodes.InvalidCsv);

    private static string? Field(string[] fields, int index) => index < fields.Length ? fields[index] : null;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
