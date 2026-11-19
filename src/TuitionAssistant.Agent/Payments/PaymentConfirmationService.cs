using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Orchestration;
using TuitionAssistant.Agent.Tools;

namespace TuitionAssistant.Agent.Payments;

public sealed record PaymentOutcome(string Status, string Answer, object? Payment, AgentTrace Trace);

/// <summary>
/// Executes a pending payment after the student pressed Confirm. This is the ONLY place that calls the Pay API.
/// A pending payment is single-use, expires, and can only be confirmed by the student who owns the session.
/// </summary>
public sealed class PaymentConfirmationService(
    PendingPaymentStore store,
    ITuitionGatewayClient gateway,
    ConversationStore conversations,
    ITraceWriter traceWriter,
    IOptions<AgentOptions> options,
    TimeProvider clock)
{
    public async Task<PaymentOutcome> ConfirmAsync(Guid pendingId, string sessionId, ToolContext context, CancellationToken cancellationToken)
    {
        var pending = Authorize(pendingId, sessionId, context);
        var now = clock.GetUtcNow();

        // Mark as used BEFORE calling the API, under a lock, so a double click can never pay twice.
        lock (store.SyncRoot)
        {
            if (pending.State != PendingState.Pending)
            {
                throw new AgentException(StatusCodes.Status409Conflict, AgentErrorCodes.PendingAlreadyUsed, "This payment was already confirmed or cancelled.");
            }

            if (now >= pending.ExpiresAt)
            {
                throw new AgentException(StatusCodes.Status410Gone, AgentErrorCodes.PendingExpired, "This payment request expired. Please ask the assistant to prepare it again.");
            }

            pending.State = PendingState.Used;
        }

        var trace = new AgentTrace
        {
            SessionId = sessionId,
            StudentNo = context.StudentNo,
            TimestampUtc = now,
            Model = "n/a",
            Question = $"Confirm payment {pending.PendingId}",
        };
        trace.Add(new TraceStep
        {
            Kind = "confirmation",
            Name = "user_confirmed_payment",
            Input = new { pendingId = pending.PendingId, pending.Term, pending.Amount },
            Output = new { confirmedAtUtc = now },
            Status = "OK",
        });

        var transactionId = "AGT-" + Guid.NewGuid().ToString("N");
        var response = await PayWithOneRetryAsync(pending, transactionId, context, cancellationToken);
        trace.Add(new TraceStep
        {
            Kind = "rest",
            Name = "pay_tuition",
            Input = new { pending.StudentNo, pending.Term, pending.Amount, transactionId },
            Request = "POST /api/v1/banking/payments (via gateway, X-Client-Channel: ai-agent)",
            HttpStatus = response.StatusCode,
            Status = response.StatusCode == 201 ? "OK" : "ERROR",
            Output = TryParse(response.Body),
        });

        string status, answer;
        object? payment = null;
        if (response.StatusCode == (int)HttpStatusCode.Created)
        {
            var body = JsonNode.Parse(response.Body)!;
            payment = body;
            status = "SUCCESS";
            var remaining = body["remainingBalance"]?.GetValue<decimal>() ?? 0m;
            answer = FormattableString.Invariant($"Payment of ${pending.Amount:0.00} for {pending.Term} was recorded. Remaining balance: ${remaining:0.00}.");
        }
        else
        {
            // Never claim success when the Pay API refused (Policy §7.4).
            status = "FAILED";
            answer = $"The payment was not recorded: {ApiProblem.Describe(response.Body)}";
        }

        trace.FinalAnswer = answer;
        trace.Outcome = status == "SUCCESS" ? "COMPLETED" : "FAILED";

        // Let the model know the outcome in later turns.
        conversations.GetOrCreate(sessionId, context.StudentNo)
            .Append(options.Value.HistoryMaxMessages, new Llm.LlmMessage("system", $"[Payment note] {answer}"));

        await traceWriter.WriteAsync(trace, CancellationToken.None);
        return new PaymentOutcome(status, answer, payment, trace);
    }

    public void Cancel(Guid pendingId, string sessionId, ToolContext context)
    {
        var pending = Authorize(pendingId, sessionId, context);
        lock (store.SyncRoot)
        {
            if (pending.State != PendingState.Pending)
            {
                throw new AgentException(StatusCodes.Status409Conflict, AgentErrorCodes.PendingAlreadyUsed, "This payment was already confirmed or cancelled.");
            }

            pending.State = PendingState.Cancelled;
        }
    }

    private PendingPayment Authorize(Guid pendingId, string sessionId, ToolContext context)
    {
        var pending = store.Find(pendingId)
            ?? throw new AgentException(StatusCodes.Status404NotFound, AgentErrorCodes.PendingNotFound, "No such pending payment.");

        if (pending.StudentNo != context.StudentNo || pending.SessionId != sessionId)
        {
            throw new AgentException(StatusCodes.Status403Forbidden, AgentErrorCodes.SessionForbidden, "This payment belongs to another student or session.");
        }

        return pending;
    }

    private async Task<GatewayResponse> PayWithOneRetryAsync(PendingPayment pending, string transactionId, ToolContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await gateway.PayAsync(pending.StudentNo, pending.Term, pending.Amount, transactionId, context.BearerToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Transient network failure: retry once with the SAME transaction id (idempotency key).
            try
            {
                var retry = await gateway.PayAsync(pending.StudentNo, pending.Term, pending.Amount, transactionId, context.BearerToken, cancellationToken);
                if (retry.StatusCode == (int)HttpStatusCode.Conflict && ApiProblem.Code(retry.Body) == "DUPLICATE_TRANSACTION"
                    && OriginalMatches(retry.Body, pending))
                {
                    // The first attempt landed; the original payment is the one we wanted.
                    return new GatewayResponse(201, new JsonObject
                    {
                        ["status"] = "SUCCESS",
                        ["remainingBalance"] = pending.BalanceAtPreparation - pending.Amount,
                    }.ToJsonString());
                }

                return retry;
            }
            catch (HttpRequestException)
            {
                throw new AgentException(StatusCodes.Status502BadGateway, AgentErrorCodes.Upstream, "The payment service is not reachable. The payment was not confirmed; please check your balance before trying again.");
            }
        }
    }

    private static bool OriginalMatches(string body, PendingPayment pending)
    {
        var original = JsonNode.Parse(body)?["original"];
        return original?["studentNo"]?.GetValue<string>() == pending.StudentNo
            && original["term"]?.GetValue<string>() == pending.Term
            && original["amount"]?.GetValue<decimal>() == pending.Amount;
    }

    private static object? TryParse(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }
    }
}
