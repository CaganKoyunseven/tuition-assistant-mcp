using System.Text.Json;
using System.Text.RegularExpressions;

namespace TuitionAssistant.Agent.Llm;

/// <summary>
/// Offline stand-in for the LLM (<c>Llm__Provider=Fake</c>): picks tools with simple keyword rules and
/// answers by quoting tool results. It lets the whole stack be demonstrated without an API key.
/// The graded mode is the real LLM.
/// </summary>
public sealed partial class FakeLlmClient : ILlmClient
{
    public Task<LlmResponse> CompleteAsync(string model, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken)
    {
        var lastUser = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        var toolResults = messages.Reverse().TakeWhile(m => m.Role != "user").Where(m => m.Role == "tool").Reverse().ToList();
        var usage = new LlmUsage(Math.Max(1, messages.Sum(m => (m.Content?.Length ?? 0) / 4)), 20);

        if (toolResults.Count > 0)
        {
            var answer = "(offline demo model) Tool results:\n" + string.Join("\n", toolResults.Select(m => "- " + m.Content));
            return Task.FromResult(new LlmResponse(answer, [], "stop", usage));
        }

        var text = lastUser.ToLowerInvariant();
        var calls = new List<ToolCall>();
        var names = tools.Select(t => t.Name).ToHashSet();

        var pay = PayRegex().Match(text);
        if (pay.Success && names.Contains("prepare_payment"))
        {
            var term = TermRegex().Match(lastUser.ToUpperInvariant());
            var args = new Dictionary<string, object?> { ["term"] = term.Success ? term.Value : "2026-FALL", ["amount"] = decimal.Parse(pay.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) };
            calls.Add(new ToolCall("fake-1", "prepare_payment", JsonSerializer.Serialize(args)));
        }
        else
        {
            if (AccountWords.Any(text.Contains) && names.Contains("get_my_tuition"))
            {
                calls.Add(new ToolCall("fake-1", "get_my_tuition", "{}"));
            }

            if (PolicyWords.Any(text.Contains) && names.Contains("search_tuition_policies"))
            {
                calls.Add(new ToolCall("fake-2", "search_tuition_policies", JsonSerializer.Serialize(new { query = lastUser, top_k = 3 })));
            }
        }

        return Task.FromResult(calls.Count > 0
            ? new LlmResponse(null, calls, "tool_calls", usage)
            : new LlmResponse("(offline demo model) I can answer tuition policy questions and show your balance.", [], "stop", usage));
    }

    private static readonly string[] AccountWords = ["balance", "owe", "my tuition", "how much", "paid"];
    private static readonly string[] PolicyWords = ["policy", "deadline", "installment", "late", "fee", "refund", "scholarship", "rule", "can i"];

    [GeneratedRegex(@"pay\D{0,20}?\$?(\d+(?:\.\d{1,2})?)")]
    private static partial Regex PayRegex();

    [GeneratedRegex(@"\d{4}-(FALL|SPRING|SUMMER)")]
    private static partial Regex TermRegex();
}
