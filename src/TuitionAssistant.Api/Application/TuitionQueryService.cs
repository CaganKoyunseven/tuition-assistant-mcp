using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Application;

/// <summary>Query Tuition logic shared by the mobile and banking endpoints.</summary>
public class TuitionQueryService(TuitionDbContext db)
{
    /// <param name="studentNo">Validated student number.</param>
    /// <param name="term">Canonical term, or null for the overall balance.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    public async Task<TuitionQueryResponse> GetAsync(string studentNo, string? term, CancellationToken cancellationToken)
    {
        if (!await db.Students.AnyAsync(s => s.StudentNo == studentNo, cancellationToken))
        {
            throw ApiException.NotFound(ErrorCodes.StudentNotFound, "Student not found", $"No student with number {studentNo} exists.");
        }

        var records = await db.TuitionRecords.AsNoTracking()
            .Where(r => r.StudentNo == studentNo && (term == null || r.Term == term))
            .ToListAsync(cancellationToken);

        if (term is not null && records.Count == 0)
        {
            throw ApiException.NotFound(ErrorCodes.TuitionNotFound, "Tuition not found", $"No tuition record for student {studentNo} in term {term}.");
        }

        var terms = records
            .OrderBy(r => Term.TryParse(r.Term, out var parsed) ? parsed : default)
            .Select(ToDto)
            .ToList();

        var totalCents = records.Sum(r => r.AmountCents);
        var paidCents = records.Sum(r => r.PaidCents);

        return new TuitionQueryResponse
        {
            StudentNo = studentNo,
            Scope = term is null ? "OVERALL" : "TERM",
            Term = term,
            TuitionTotal = Money.FromCents(totalCents),
            PaidAmount = Money.FromCents(paidCents),
            Balance = Money.FromCents(totalCents - paidCents),
            Status = records.Count == 0 ? TuitionStatuses.NoTuition : TuitionStatuses.From(totalCents, paidCents),
            Terms = terms,
        };
    }

    private static TermTuitionDto ToDto(TuitionRecord record) => new()
    {
        Term = record.Term,
        TuitionTotal = Money.FromCents(record.AmountCents),
        PaidAmount = Money.FromCents(record.PaidCents),
        Balance = Money.FromCents(record.BalanceCents),
        Status = record.Status,
    };
}
