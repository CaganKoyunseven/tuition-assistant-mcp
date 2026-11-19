using System.Text.Json;
using TuitionAssistant.Agent.Llm;

namespace TuitionAssistant.Agent.Tools;

/// <summary>The three tools the model may use: the MCP tool(s) discovered at runtime plus two local ones.</summary>
public sealed class ToolRegistry(IPolicySearchClient mcp, GetMyTuitionTool tuitionTool, PreparePaymentTool paymentTool)
{
    public async Task<IReadOnlyList<ToolDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var discovered = await mcp.ListToolsAsync(cancellationToken);
        return [.. discovered, tuitionTool.Definition, paymentTool.Definition];
    }

    public async Task<ToolOutcome> ExecuteAsync(ToolCall call, ToolContext context, IReadOnlyCollection<ToolDefinition> known, CancellationToken cancellationToken)
    {
        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new ToolOutcome("local", ToolErrors.Json("INVALID_ARGUMENTS", "The tool arguments were not valid JSON."), "ERROR");
        }

        try
        {
            if (call.Name == GetMyTuitionTool.Name)
            {
                return await tuitionTool.ExecuteAsync(arguments, context, cancellationToken);
            }

            if (call.Name == PreparePaymentTool.Name)
            {
                return await paymentTool.ExecuteAsync(arguments, context, cancellationToken);
            }

            if (known.Any(t => t.Name == call.Name))
            {
                var result = await mcp.CallToolAsync(call.Name, arguments, cancellationToken);
                return result.IsError
                    ? new ToolOutcome("mcp", ToolErrors.Json("MCP_TOOL_ERROR", result.Text), "ERROR")
                    : new ToolOutcome("mcp", result.Text, "OK");
            }

            return new ToolOutcome("local", ToolErrors.Json("UNKNOWN_TOOL", $"There is no tool named '{call.Name}'."), "ERROR");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ModelContextProtocol.McpException or IOException)
        {
            return new ToolOutcome(call.Name == GetMyTuitionTool.Name ? "rest" : "mcp",
                ToolErrors.Json("TOOL_UNAVAILABLE", "The service behind this tool is not reachable right now."), "ERROR");
        }
    }
}
