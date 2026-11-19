using System.Text.Json;
using TuitionAssistant.Agent.Llm;
using TuitionAssistant.Agent.Payments;

namespace TuitionAssistant.Agent.Tools;

/// <summary>The signed-in student. The student number always comes from the validated JWT, never from the model.</summary>
public sealed record ToolContext(string StudentNo, string BearerToken, string SessionId);

/// <summary>Result of one tool execution. <see cref="Json"/> is what the model sees.</summary>
public sealed record ToolOutcome(string Kind, string Json, string Status, int? HttpStatus = null, string? Request = null, PendingPayment? Pending = null);

/// <summary>A tool the model can call.</summary>
public interface IAgentTool
{
    ToolDefinition Definition { get; }

    Task<ToolOutcome> ExecuteAsync(JsonElement arguments, ToolContext context, CancellationToken cancellationToken);
}

/// <summary>Builds the JSON the model receives when a tool fails (never stack traces).</summary>
public static class ToolErrors
{
    public static string Json(string code, string message, int? httpStatus = null) =>
        JsonSerializer.Serialize(new { error = new { code, message, httpStatus } });
}
