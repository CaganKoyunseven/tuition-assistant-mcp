using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Llm;

namespace TuitionAssistant.Agent.Tools;

/// <summary>Result of an MCP tool call: the text payload and whether the server flagged an error.</summary>
public sealed record McpCallResult(string Text, bool IsError);

/// <summary>Client for the MCP policy server. Tools are discovered with <c>tools/list</c>, never hardcoded.</summary>
public interface IPolicySearchClient
{
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken);

    Task<McpCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken);
}

/// <summary>Streamable HTTP MCP client. The connection is opened lazily and re-opened after a failure.</summary>
public sealed class McpPolicySearchClient(IOptions<McpOptions> options) : IPolicySearchClient, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;

    public async Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken);
        try
        {
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return tools.Select(t => new ToolDefinition(
                t.Name,
                t.Description ?? t.Name,
                JsonNode.Parse(t.JsonSchema.GetRawText())!)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ResetAsync();
            throw;
        }
    }

    public async Task<McpCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken);
        var args = new Dictionary<string, object?>();
        if (arguments.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in arguments.EnumerateObject())
            {
                args[property.Name] = property.Value.Clone();
            }
        }

        try
        {
            var result = await client.CallToolAsync(name, args, cancellationToken: cancellationToken);
            var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            return new McpCallResult(text, result.IsError == true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ResetAsync();
            throw;
        }
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _client ??= await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = new Uri(options.Value.Url),
                    TransportMode = HttpTransportMode.StreamableHttp,
                    AdditionalHeaders = new Dictionary<string, string> { ["X-Mcp-Api-Key"] = options.Value.ApiKey ?? "" },
                }),
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}
