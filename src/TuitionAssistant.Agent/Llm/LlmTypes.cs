using System.Text.Json.Nodes;

namespace TuitionAssistant.Agent.Llm;

/// <summary>A function call requested by the model.</summary>
public sealed record ToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>One chat message. Roles: <c>system</c>, <c>user</c>, <c>assistant</c>, <c>tool</c>.</summary>
public sealed record LlmMessage(string Role, string? Content, IReadOnlyList<ToolCall>? ToolCalls = null, string? ToolCallId = null);

/// <summary>A function the model may call (JSON-schema parameters).</summary>
public sealed record ToolDefinition(string Name, string Description, JsonNode ParametersSchema);

public sealed record LlmUsage(int InputTokens, int OutputTokens);

public sealed record LlmResponse(string? Text, IReadOnlyList<ToolCall> ToolCalls, string FinishReason, LlmUsage Usage);

/// <summary>The LLM could not be reached or returned an error.</summary>
public sealed class LlmException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Chat Completions style client. The model is passed per call so callers can choose one.</summary>
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(string model, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken);
}
