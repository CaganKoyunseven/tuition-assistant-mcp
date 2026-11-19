using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TuitionAssistant.Agent.Llm;
using TuitionAssistant.Agent.Payments;

namespace TuitionAssistant.Agent.Tools;

/// <summary>
/// Prepares a payment for confirmation. It validates against the live balance and creates a pending payment,
/// but never pays: only the Confirm endpoint (a button click by the student) calls the Pay API.
/// </summary>
public sealed partial class PreparePaymentTool(ITuitionGatewayClient gateway, PendingPaymentStore store) : IAgentTool
{
    public const string Name = "prepare_payment";

    public ToolDefinition Definition { get; } = new(
        Name,
        "Prepare a tuition payment for the signed-in student. This does NOT pay; the student must press the Confirm button.",
        JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "term": { "type": "string", "description": "Term to pay, e.g. 2026-FALL." },
            "amount": { "type": "number", "description": "Amount in USD, greater than 0, at most two decimals, not above the term balance." }
          },
          "required": ["term", "amount"],
          "additionalProperties": false
        }
        """)!);

    public async Task<ToolOutcome> ExecuteAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var term = ToolArguments.GetString(arguments, "term");
        var amount = ToolArguments.GetDecimal(arguments, "amount");

        if (term is null || !TermRegex().IsMatch(term))
        {
            return Fail("INVALID_TERM", "The term must look like 2026-FALL, 2026-SPRING or 2026-SUMMER.");
        }

        if (amount is not { } value || value <= 0 || decimal.Round(value, 2) != value)
        {
            return Fail("INVALID_AMOUNT", "The amount must be greater than 0 and have at most two decimals.");
        }

        var request = $"GET /api/v1/banking/tuition/{context.StudentNo}?term={term} (via gateway)";
        var response = await gateway.QueryTuitionAsync(context.StudentNo, term, context.BearerToken, cancellationToken);
        if (response.StatusCode is < 200 or >= 300)
        {
            return new ToolOutcome("local", ToolErrors.Json("API_ERROR", ApiProblem.Describe(response.Body), response.StatusCode), "ERROR", response.StatusCode, request);
        }

        var balance = JsonNode.Parse(response.Body)?["balance"]?.GetValue<decimal>() ?? 0m;
        if (balance <= 0)
        {
            return Fail("NOTHING_TO_PAY", $"Term {term} has no outstanding balance.", request);
        }

        if (value > balance)
        {
            return Fail("AMOUNT_EXCEEDS_BALANCE", FormattableString.Invariant($"The amount {value:0.00} is higher than the outstanding balance {balance:0.00} for {term}."), request);
        }

        var pending = store.Create(context.SessionId, context.StudentNo, term, value, balance);
        return new ToolOutcome("local", JsonSerializer.Serialize(pending.ToSummary()), "OK", null, request, pending);
    }

    private static ToolOutcome Fail(string code, string message, string? request = null) =>
        new("local", ToolErrors.Json(code, message), "ERROR", null, request);

    [GeneratedRegex(@"^\d{4}-(FALL|SPRING|SUMMER)$")]
    private static partial Regex TermRegex();
}
