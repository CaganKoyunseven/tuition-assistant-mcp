using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Application;

/// <summary>Unpaid Tuition Status: paged list of students with a balance &gt; 0 for a term.</summary>
public class UnpaidReportService(TuitionDbContext db)
{
    public async Task<UnpaidTuitionResponse> GetAsync(string? term, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw ApiException.Validation("The 'term' query parameter is required (e.g. 2026-FALL).");
        }

        var canonicalTerm = Validation.Term(term);
        var (pageNumber, size) = Validation.Paging(page, pageSize);

        var unpaid = db.TuitionRecords.AsNoTracking()
            .Where(r => r.Term == canonicalTerm && r.AmountCents > r.PaidCents);

        var totalCount = await unpaid.CountAsync(cancellationToken);
        var outstandingCents = await unpaid.SumAsync(r => (long?)(r.AmountCents - r.PaidCents), cancellationToken) ?? 0;

        var skip = (long)(pageNumber - 1) * size;
        var items = skip >= totalCount
            ? []
            : await unpaid
                // Balance is stored as integer cents, so this ORDER BY works in every provider.
                .OrderByDescending(r => r.AmountCents - r.PaidCents)
                .ThenBy(r => r.StudentNo)
                .Skip((int)skip)
                .Take(size)
                .Select(r => new { r.StudentNo, r.Student!.FullName, r.AmountCents, r.PaidCents })
                .ToListAsync(cancellationToken);

        return new UnpaidTuitionResponse
        {
            Term = canonicalTerm,
            Page = pageNumber,
            PageSize = size,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)size),
            TotalOutstanding = Money.FromCents(outstandingCents),
            Items = items.Select(r => new UnpaidTuitionItem
            {
                StudentNo = r.StudentNo,
                FullName = r.FullName,
                TuitionTotal = Money.FromCents(r.AmountCents),
                PaidAmount = Money.FromCents(r.PaidCents),
                Balance = Money.FromCents(r.AmountCents - r.PaidCents),
                Status = TuitionStatuses.From(r.AmountCents, r.PaidCents),
            }).ToList(),
        };
    }
}
