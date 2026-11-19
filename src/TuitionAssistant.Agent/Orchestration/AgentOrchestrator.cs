using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Llm;
using TuitionAssistant.Agent.Payments;
using TuitionAssistant.Agent.Tools;

namespace TuitionAssistant.Agent.Orchestration;

public sealed record ChatResult(string SessionId, string Answer, string Model, AgentTrace Trace, object? PendingPayment);

/// <summary>
/// The tool-calling loop: ask the model, run the tools it requests, give the results back,
/// repeat until it answers (or the iteration limit is reached).
/// </summary>
public sealed class AgentOrchestrator(
    ILlmClient llm,
    ToolRegistry tools,
    ConversationStore conversations,
    ITraceWriter traceWriter,
    IOptions<LlmOptions> llmOptions,
    IOptions<AgentOptions> agentOptions,
    TimeProvider clock)
{
    private const int ToolOutputTraceLimit = 4096;

    public async Task<ChatResult> ChatAsync(string sessionId, string message, string? requestedModel, ToolContext context, CancellationToken cancellationToken)
    {
        var settings = llmOptions.Value;
        var model = ResolveModel(settings, requestedModel);
        var conversation = conversations.GetOrCreate(sessionId, context.StudentNo);

        var trace = new AgentTrace
        {
            SessionId = sessionId,
            StudentNo = context.StudentNo,
            TimestampUtc = clock.GetUtcNow(),
            Model = model,
            Question = message,
        };

        var messages = new List<LlmMessage> { new("system", SystemPrompt.Build(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), context.StudentNo)) };
        messages.AddRange(conversation.Snapshot());
        messages.Add(new LlmMessage("user", message));

        PendingPayment? pending = null;
        string answer;
        try
        {
            var definitions = await tools.GetDefinitionsAsync(cancellationToken);
            answer = "I could not complete this request within the allowed number of steps. Please try rephrasing your question.";
            trace.Outcome = "LIMIT_REACHED";

            for (var iteration = 0; iteration < Math.Max(1, settings.MaxToolIterations); iteration++)
            {
                var watch = Stopwatch.StartNew();
                var response = await llm.CompleteAsync(model, messages, definitions, cancellationToken);
                watch.Stop();
                AddLlmStep(trace, settings, model, response, watch.ElapsedMilliseconds);

                if (response.ToolCalls.Count == 0)
                {
                    answer = response.Text ?? "";
                    trace.Outcome = "COMPLETED";
                    break;
                }

                messages.Add(new LlmMessage("assistant", response.Text, response.ToolCalls));
                foreach (var call in response.ToolCalls)
                {
                    watch.Restart();
                    var outcome = await tools.ExecuteAsync(call, context, definitions, cancellationToken);
                    watch.Stop();
                    pending = outcome.Pending ?? pending;
                    trace.Add(new TraceStep
                    {
                        Kind = outcome.Kind,
                        Name = call.Name,
                        Input = ParseOrText(call.ArgumentsJson),
                        Request = outcome.Request,
                        HttpStatus = outcome.HttpStatus,
                        Status = outcome.Status,
                        Output = TraceOutput(outcome),
                        DurationMs = watch.ElapsedMilliseconds,
                    });
                    messages.Add(new LlmMessage("tool", outcome.Json, ToolCallId: call.Id));
                }
            }
        }
        catch (Exception ex) when (ex is LlmException or HttpRequestException or ModelContextProtocol.McpException or TaskCanceledException)
        {
            trace.Outcome = "FAILED";
            trace.FinalAnswer = ex.Message;
            await traceWriter.WriteAsync(trace, CancellationToken.None);
            throw new AgentException(StatusCodes.Status502BadGateway, AgentErrorCodes.Upstream,
                ex is LlmException ? ex.Message : "A backend service is not reachable right now. Please try again shortly.");
        }

        trace.FinalAnswer = answer;
        conversation.Append(agentOptions.Value.HistoryMaxMessages, new LlmMessage("user", message), new LlmMessage("assistant", answer));
        await traceWriter.WriteAsync(trace, CancellationToken.None);
        return new ChatResult(sessionId, answer, model, trace, pending?.ToSummary());
    }

    /// <summary>The default model, or a caller's choice if it is on the allow-list.</summary>
    public static string ResolveModel(LlmOptions settings, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return string.IsNullOrWhiteSpace(settings.Model)
                ? throw new AgentException(StatusCodes.Status500InternalServerError, AgentErrorCodes.Upstream, "No LLM model is configured (set LLM_MODEL in .env).")
                : settings.Model;
        }

        return settings.AvailableModels().Contains(requested.Trim(), StringComparer.Ordinal)
            ? requested.Trim()
            : throw new AgentException(StatusCodes.Status400BadRequest, AgentErrorCodes.ModelNotAllowed,
                $"Model '{requested}' is not available. Choose one of: {string.Join(", ", settings.AvailableModels())}.");
    }

    private static void AddLlmStep(AgentTrace trace, LlmOptions settings, string model, LlmResponse response, long durationMs)
    {
        trace.Totals.InputTokens += response.Usage.InputTokens;
        trace.Totals.OutputTokens += response.Usage.OutputTokens;
        trace.Totals.EstimatedCostUsd = decimal.Round(
            (trace.Totals.InputTokens * settings.InputPricePerMillion + trace.Totals.OutputTokens * settings.OutputPricePerMillion) / 1_000_000m, 6);

        var isFinal = response.ToolCalls.Count == 0;
        trace.Add(new TraceStep
        {
            Kind = "llm",
            Name = isFinal ? "final_answer" : "tool_selection",
            Input = new { model },
            Output = isFinal
                ? new { finishReason = response.FinishReason }
                : new { finishReason = response.FinishReason, toolCalls = response.ToolCalls.Select(c => c.Name).ToArray() },
            Usage = new { input = response.Usage.InputTokens, output = response.Usage.OutputTokens },
            DurationMs = durationMs,
        });
    }

    /// <summary>
    /// What the trace records as a tool's result. MCP results are long, so they are summarised
    /// (section, heading, score, short excerpt) instead of being cut in the middle of the JSON.
    /// </summary>
    private static object TraceOutput(ToolOutcome outcome)
    {
        if (outcome.Kind == "mcp" && outcome.Status == "OK")
        {
            try
            {
                var root = JsonNode.Parse(outcome.Json)!;
                var matches = (root["matches"]?.AsArray() ?? []).Select(m => new
                {
                    section = m?["section"]?.GetValue<string>(),
                    heading = m?["heading"]?.GetValue<string>(),
                    score = m?["score"]?.GetValue<double>(),
                    excerpt = Truncate(m?["text"]?.GetValue<string>() ?? "", 240),
                }).ToList();
                return new { query = root["query"]?.GetValue<string>(), totalMatches = root["totalMatches"]?.GetValue<int>() ?? matches.Count, message = root["message"]?.GetValue<string>(), matches };
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // fall through to the generic form
            }
        }

        return ParseOrText(Truncate(outcome.Json, ToolOutputTraceLimit));
    }

    private static object ParseOrText(string json)
    {
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
