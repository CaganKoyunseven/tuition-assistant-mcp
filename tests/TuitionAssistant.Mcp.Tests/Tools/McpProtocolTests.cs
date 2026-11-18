using System.Net;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using TuitionAssistant.Mcp.Tests.Infrastructure;
using TuitionAssistant.Mcp.Tools;

namespace TuitionAssistant.Mcp.Tests.Tools;

/// <summary>End-to-end over Streamable HTTP with the official MCP C# client.</summary>
public class McpProtocolTests(McpServerFactory factory) : IClassFixture<McpServerFactory>
{
    [Fact]
    public async Task ToolsList_ContainsSearchToolWithInputSchema()
    {
        await using var client = await factory.CreateMcpClientAsync();

        var tools = await client.ListToolsAsync();

        var tool = Assert.Single(tools);
        Assert.Equal(TuitionPolicyTools.SearchToolName, tool.Name);
        Assert.Contains("UNI-TUITION-2026-01", tool.Description);
        var schema = tool.JsonSchema;
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal("string", schema.GetProperty("properties").GetProperty("query").GetProperty("type").GetString());
        Assert.Equal("integer", schema.GetProperty("properties").GetProperty("top_k").GetProperty("type").GetString());
        Assert.Contains("query", schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        var query = schema.GetProperty("properties").GetProperty("query");
        Assert.Equal(2, query.GetProperty("minLength").GetInt32());
        Assert.Equal(300, query.GetProperty("maxLength").GetInt32());
        var topK = schema.GetProperty("properties").GetProperty("top_k");
        Assert.Equal(1, topK.GetProperty("minimum").GetInt32());
        Assert.Equal(5, topK.GetProperty("maximum").GetInt32());
        Assert.Equal(3, topK.GetProperty("default").GetInt32());
        Assert.Equal("tuition-policy-mcp", client.ServerInfo?.Name);
    }

    [Fact]
    public async Task ToolsCall_ReturnsRelevantExcerptsAsJsonText()
    {
        await using var client = await factory.CreateMcpClientAsync();

        var result = await client.CallToolAsync(TuitionPolicyTools.SearchToolName,
            new Dictionary<string, object?> { ["query"] = "payment deadline fall term", ["top_k"] = 2 });

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var root = JsonDocument.Parse(text).RootElement;
        Assert.Equal("2.1", root.GetProperty("matches")[0].GetProperty("section").GetString());
        Assert.Equal(2, root.GetProperty("totalMatches").GetInt32());
    }

    [Fact]
    public async Task ToolsCall_InvalidInput_ReturnsToolErrorInsteadOfCrashing()
    {
        await using var client = await factory.CreateMcpClientAsync();

        var result = await client.CallToolAsync(TuitionPolicyTools.SearchToolName,
            new Dictionary<string, object?> { ["query"] = "installments", ["top_k"] = 9 });

        Assert.True(result.IsError);
        Assert.Contains("top_k", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public async Task ManyToolCalls_UseTheSameIndexBuiltOnce()
    {
        var before = factory.Index;
        await using var client = await factory.CreateMcpClientAsync();

        for (var i = 0; i < 20; i++)
        {
            await client.CallToolAsync(TuitionPolicyTools.SearchToolName, new Dictionary<string, object?> { ["query"] = "late fee" });
        }

        Assert.Same(before, factory.Index);
        Assert.Equal(1, factory.Index.BuildCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task McpEndpoint_WithoutValidApiKey_Returns401(string? apiKey)
    {
        var http = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (apiKey is not null)
        {
            request.Headers.Add("X-Mcp-Api-Key", apiKey);
        }

        var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpClient_WithoutApiKey_CannotConnect()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => factory.CreateMcpClientAsync(apiKey: null));
    }
}
