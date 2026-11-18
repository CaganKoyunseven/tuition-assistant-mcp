using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;

namespace TuitionAssistant.Api.Application;

/// <summary>
/// Add Tuition: an idempotent upsert keyed by (studentNo, term).
/// Re-sending the same record is safe (UNCHANGED). A different amount updates the assessed total,
/// unless it would fall below the amount already paid (422 AMOUNT_BELOW_PAID).
/// Unknown student numbers are created automatically (the specification has no "create student" API).
/// </summary>
public class TuitionAdminService(TuitionDbContext db, TimeProvider clock)
{
    public const string Created = "CREATED";
    public const string Updated = "UPDATED";
    public const string Unchanged = "UNCHANGED";

    public async Task<AddTuitionResponse> AddOrUpdateAsync(AddTuitionRequest request, CancellationToken cancellationToken)
    {
        var studentNo = Validation.StudentNo(request.StudentNo);
        var term = Validation.Term(request.Term);
        var amountCents = Validation.Amount(request.Amount);
        var currency = Validation.Currency(request.Currency);
        var now = clock.GetUtcNow().UtcDateTime;

        try
        {
            var studentCreated = false;
            if (!await db.Students.AnyAsync(s => s.StudentNo == studentNo, cancellationToken))
            {
                db.Students.Add(new Student { StudentNo = studentNo, FullName = null, CreatedAt = now });
                studentCreated = true;
            }

            var record = await db.TuitionRecords.SingleOrDefaultAsync(r => r.StudentNo == studentNo && r.Term == term, cancellationToken);
            string status;
            if (record is null)
            {
                record = new TuitionRecord
                {
                    Id = Guid.NewGuid(),
                    StudentNo = studentNo,
                    Term = term,
                    AmountCents = amountCents,
                    PaidCents = 0,
                    Currency = currency,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.TuitionRecords.Add(record);
                status = Created;
            }
            else if (record.AmountCents == amountCents)
            {
                status = Unchanged;
            }
            else if (amountCents < record.PaidCents)
            {
                throw ApiException.BusinessRule(ErrorCodes.AmountBelowPaid, "Amount below paid amount",
                        $"New tuition total {Money.FromCents(amountCents)} is below the amount already paid ({Money.FromCents(record.PaidCents)}) for {term}.")
                    .With("paidAmount", Money.FromCents(record.PaidCents));
            }
            else
            {
                record.AmountCents = amountCents;
                record.ConcurrencyStamp = Guid.NewGuid();
                record.UpdatedAt = now;
                status = Updated;
            }

            await db.SaveChangesAsync(cancellationToken);

            return new AddTuitionResponse
            {
                Status = status,
                StudentNo = studentNo,
                Term = term,
                TuitionTotal = Money.FromCents(record.AmountCents),
                PaidAmount = Money.FromCents(record.PaidCents),
                Balance = Money.FromCents(record.BalanceCents),
                Currency = record.Currency,
                StudentCreated = studentCreated,
            };
        }
        catch (DbUpdateException)
        {
            // Concurrent create of the same (studentNo, term) or a concurrent payment changed the record.
            throw ApiException.Conflict(ErrorCodes.ConcurrencyConflict, "Concurrency conflict",
                "The tuition record was changed by another request. Please retry.");
        }
        finally
        {
            // Leave the context clean for the next call (the batch import reuses it row by row).
            db.ChangeTracker.Clear();
        }
    }
}
