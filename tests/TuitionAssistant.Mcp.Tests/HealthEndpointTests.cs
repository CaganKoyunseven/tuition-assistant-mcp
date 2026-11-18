using System.Net;
using System.Text.Json;
using TuitionAssistant.Mcp.Tests.Infrastructure;

namespace TuitionAssistant.Mcp.Tests;

public class HealthEndpointTests(McpServerFactory factory) : IClassFixture<McpServerFactory>
{
    [Fact]
    public async Task Health_IsPublicAndReportsIndexedDocument()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("tuition-policy-mcp", root.GetProperty("service").GetString());
        Assert.Equal("UNI-TUITION-2026-01", root.GetProperty("documentId").GetString());
        Assert.Equal(26, root.GetProperty("chunks").GetInt32());
        Assert.Equal(64, root.GetProperty("documentSha256").GetString()!.Length);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        var response = await factory.CreateClient().GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
