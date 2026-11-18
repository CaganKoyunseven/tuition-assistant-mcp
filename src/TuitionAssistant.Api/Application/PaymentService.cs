using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Application;

/// <summary>
/// Pay Tuition: records a simulated payment (no card processing). Partial and full payments are
/// allowed; duplicate transaction IDs and amounts above the balance are rejected.
/// </summary>
public class PaymentService(TuitionDbContext db, TimeProvider clock)
{
    /// <summary>Header the AI agent sends so its payments are recorded with channel AI_AGENT.</summary>
    public const string ClientChannelHeader = "X-Client-Channel";

    public async Task<PaymentResponse> PayAsync(PayTuitionRequest request, ClaimsPrincipal user, string? clientChannel, CancellationToken cancellationToken)
    {
        var studentNo = Validation.StudentNo(request.StudentNo);
        var term = Validation.Term(request.Term);
        var amountCents = Validation.Amount(request.Amount);
        var transactionId = Validation.TransactionId(request.TransactionId);

        StudentAccess.EnsureCanAccessStudent(user, studentNo);
        var channel = ResolveChannel(user, clientChannel);

        // One retry on an optimistic-concurrency conflict (another payment changed the record in between).
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await PayOnceAsync(studentNo, term, amountCents, transactionId, channel, user, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                throw ApiException.Conflict(ErrorCodes.ConcurrencyConflict, "Concurrency conflict",
                    "The tuition record was changed by another request. Please retry.");
            }
            catch (DbUpdateException)
            {
                // A concurrent request inserted the same transaction ID first (unique index).
                db.ChangeTracker.Clear();
                var original = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);
                if (original is null)
                {
                    throw;
                }

                throw Duplicate(original, user);
            }
        }
    }

    private async Task<PaymentResponse> PayOnceAsync(
        string studentNo, string term, long amountCents, string transactionId, string channel, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);
        if (existing is not null)
        {
            throw Duplicate(existing, user);
        }

        var record = await db.TuitionRecords.SingleOrDefaultAsync(r => r.StudentNo == studentNo && r.Term == term, cancellationToken)
            ?? throw ApiException.NotFound(ErrorCodes.TuitionNotFound, "Tuition not found", $"No tuition record for student {studentNo} in term {term}.");

        if (record.BalanceCents == 0)
        {
            throw ApiException.BusinessRule(ErrorCodes.AlreadyPaid, "Already paid", $"Tuition for {term} is already fully paid.");
        }

        if (amountCents > record.BalanceCents)
        {
            throw ApiException.BusinessRule(ErrorCodes.AmountExceedsBalance, "Amount exceeds balance",
                    $"Requested amount {Money.FromCents(amountCents)} exceeds the outstanding balance {Money.FromCents(record.BalanceCents)} for term {term}.")
                .With("currentBalance", Money.FromCents(record.BalanceCents));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            TuitionRecordId = record.Id,
            StudentNo = studentNo,
            Term = term,
            AmountCents = amountCents,
            Currency = record.Currency,
            Channel = channel,
            PerformedBy = StudentAccess.Subject(user),
            RecordedAt = now,
        };

        record.PaidCents += amountCents;
        record.ConcurrencyStamp = Guid.NewGuid();
        record.UpdatedAt = now;
        db.Payments.Add(payment);

        // A single SaveChanges is one database transaction: payment insert + balance update together.
        await db.SaveChangesAsync(cancellationToken);

        return new PaymentResponse
        {
            PaymentId = payment.Id,
            TransactionId = transactionId,
            StudentNo = studentNo,
            Term = term,
            Currency = record.Currency,
            PaidAmount = Money.FromCents(amountCents),
            TotalPaid = Money.FromCents(record.PaidCents),
            RemainingBalance = Money.FromCents(record.BalanceCents),
            RecordStatus = record.Status,
            Channel = channel,
            RecordedAt = now,
        };
    }

    private static string ResolveChannel(ClaimsPrincipal user, string? clientChannel)
    {
        if (user.IsInRole(Roles.Bank))
        {
            return PaymentChannels.BankApp;
        }

        return string.Equals(clientChannel, "ai-agent", StringComparison.OrdinalIgnoreCase)
            ? PaymentChannels.AiAgent
            : PaymentChannels.Student;
    }

    /// <summary>409 with the original payment, unless the original belongs to a student the caller may not see.</summary>
    private static ApiException Duplicate(Payment original, ClaimsPrincipal user)
    {
        var error = ApiException.Conflict(ErrorCodes.DuplicateTransaction, "Duplicate transaction",
            $"Transaction ID '{original.TransactionId}' has already been used.");

        var callerMaySeeOriginal = user.IsInRole(Roles.Bank)
            || user.FindFirstValue(TuitionClaims.StudentNo) == original.StudentNo;
        if (callerMaySeeOriginal)
        {
            error.With("original", new OriginalPaymentDto
            {
                PaymentId = original.Id,
                StudentNo = original.StudentNo,
                Term = original.Term,
                Amount = Money.FromCents(original.AmountCents),
                RecordedAt = original.RecordedAt,
            });
        }

        return error;
    }
}
