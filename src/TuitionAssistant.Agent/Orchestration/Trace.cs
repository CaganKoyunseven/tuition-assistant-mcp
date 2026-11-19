using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;

namespace TuitionAssistant.Agent.Orchestration;

/// <summary>One step of an agent run: an LLM call, a tool call or a payment confirmation.</summary>
public sealed class TraceStep
{
    public int Index { get; set; }

    /// <summary><c>llm</c>, <c>mcp</c>, <c>rest</c>, <c>local</c> or <c>confirmation</c>.</summary>
    public required string Kind { get; init; }

    public required string Name { get; init; }

    public object? Input { get; init; }

    public object? Output { get; init; }

    public string? Request { get; init; }

    public int? HttpStatus { get; init; }

    public string? Status { get; init; }

    public object? Usage { get; init; }

    public long DurationMs { get; init; }
}

public sealed class TraceTotals
{
    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>Full record of how an answer was produced (assignment requirement). Never contains tokens or keys.</summary>
public sealed class AgentTrace
{
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");

    public required string SessionId { get; init; }

    public required string StudentNo { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public required string Model { get; init; }

    public required string Question { get; init; }

    public List<TraceStep> Steps { get; } = [];

    public string? FinalAnswer { get; set; }

    /// <summary><c>COMPLETED</c>, <c>LIMIT_REACHED</c> or <c>FAILED</c>.</summary>
    public string Outcome { get; set; } = "COMPLETED";

    public TraceTotals Totals { get; } = new();

    public void Add(TraceStep step)
    {
        step.Index = Steps.Count + 1;
        Steps.Add(step);
    }
}

/// <summary>Persists traces so they can be shown as evidence.</summary>
public interface ITraceWriter
{
    Task WriteAsync(AgentTrace trace, CancellationToken cancellationToken);
}

public sealed class FileTraceWriter(IOptions<AgentOptions> options) : ITraceWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task WriteAsync(AgentTrace trace, CancellationToken cancellationToken)
    {
        var root = options.Value.TraceDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        try
        {
            var directory = Path.Combine(root, trace.TimestampUtc.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, trace.TraceId + ".json"), JsonSerializer.Serialize(trace, Json), cancellationToken);
        }
        catch (IOException)
        {
            // Trace files are evidence, not a dependency: a full or read-only disk must not break chat.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
