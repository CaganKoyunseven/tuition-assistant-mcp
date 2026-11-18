// Retrieval demo client for the tuition policy MCP server.
// Connects over Streamable HTTP with the official MCP C# client, lists the tools,
// then runs the retrieval demonstration queries and prints the JSON results.
//
// Usage:
//   MCP_URL=http://localhost:8081/mcp MCP_API_KEY=<key> dotnet run --project tools/TuitionAssistant.McpDemo
//   dotnet run --project tools/TuitionAssistant.McpDemo -- <url> <apiKey> ["custom query" ...]

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var url = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("MCP_URL") ?? "http://localhost:8081/mcp";
var apiKey = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("MCP_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set MCP_API_KEY (or pass the key as the second argument).");
    return 1;
}

string[] queries = args.Length > 2
    ? args[2..]
    :
    [
        "payment deadline fall term",
        "how many installments can I split my balance into",
        "refund if I withdraw early",
        "late fee percentage",
    ];

var pretty = new JsonSerializerOptions { WriteIndented = true };

await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri(url),
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = new Dictionary<string, string> { ["X-Mcp-Api-Key"] = apiKey },
}));

Console.WriteLine($"Connected to {client.ServerInfo?.Name} {client.ServerInfo?.Version} at {url}");
Console.WriteLine();
Console.WriteLine("== tools/list ==");
foreach (var tool in await client.ListToolsAsync())
{
    Console.WriteLine($"- {tool.Name}: {tool.Description}");
    Console.WriteLine($"  inputSchema: {JsonSerializer.Serialize(tool.JsonSchema, pretty).Replace("\n", "\n  ")}");
}

var number = 0;
foreach (var query in queries)
{
    number++;
    Console.WriteLine();
    Console.WriteLine($"== tools/call #{number}: search_tuition_policies(query: \"{query}\", top_k: 3) ==");
    var result = await client.CallToolAsync("search_tuition_policies", new Dictionary<string, object?> { ["query"] = query, ["top_k"] = 3 });
    var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "(no text content)";
    Console.WriteLine(result.IsError == true ? $"TOOL ERROR: {text}" : JsonSerializer.Serialize(JsonDocument.Parse(text).RootElement, pretty));
}

return 0;
