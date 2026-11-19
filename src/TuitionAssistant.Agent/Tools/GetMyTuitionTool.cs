using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TuitionAssistant.Agent.Llm;

namespace TuitionAssistant.Agent.Tools;

/// <summary>Reads the signed-in student's tuition through the gateway. There is no student-number parameter by design.</summary>
public sealed partial class GetMyTuitionTool(ITuitionGatewayClient gateway) : IAgentTool
{
    public const string Name = "get_my_tuition";

    public ToolDefinition Definition { get; } = new(
        Name,
        "Get the signed-in student's live tuition total, paid amount and outstanding balance. " +
        "Optionally restrict to one term; without a term the totals cover all terms.",
        JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "term": { "type": "string", "description": "Term in the form YYYY-FALL, YYYY-SPRING or YYYY-SUMMER, e.g. 2026-FALL. Omit for all terms." }
          },
          "additionalProperties": false
        }
        """)!);

    public async Task<ToolOutcome> ExecuteAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken)
    {
        var term = ToolArguments.GetString(arguments, "term");
        if (term is not null && !TermRegex().IsMatch(term))
        {
            return new ToolOutcome("rest", ToolErrors.Json("INVALID_TERM", "The term must look like 2026-FALL, 2026-SPRING or 2026-SUMMER."), "ERROR");
        }

        var request = $"GET /api/v1/banking/tuition/{context.StudentNo}" + (term is null ? "" : $"?term={term}") + " (via gateway)";
        var response = await gateway.QueryTuitionAsync(context.StudentNo, term, context.BearerToken, cancellationToken);
        return response.StatusCode is >= 200 and < 300
            ? new ToolOutcome("rest", response.Body, "OK", response.StatusCode, request)
            : new ToolOutcome("rest", ToolErrors.Json("API_ERROR", ApiProblem.Describe(response.Body), response.StatusCode), "ERROR", response.StatusCode, request);
    }

    [GeneratedRegex(@"^\d{4}-(FALL|SPRING|SUMMER)$")]
    private static partial Regex TermRegex();
}

/// <summary>Helpers shared by the tools.</summary>
public static class ToolArguments
{
    public static string? GetString(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim().ToUpperInvariant() is { Length: > 0 } s ? s : null
            : null;

    public static decimal? GetDecimal(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? number
            : null;
}

/// <summary>Extracts a short message from an RFC 7807 problem body.</summary>
public static class ApiProblem
{
    public static string Describe(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            return node?["detail"]?.GetValue<string>() ?? node?["title"]?.GetValue<string>() ?? "The tuition service rejected the request.";
        }
        catch (JsonException)
        {
            return "The tuition service rejected the request.";
        }
    }

    public static string? Code(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["code"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
