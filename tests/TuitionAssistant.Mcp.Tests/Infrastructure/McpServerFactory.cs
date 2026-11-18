using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using TuitionAssistant.Mcp.Indexing;
using TuitionAssistant.Mcp.Security;

namespace TuitionAssistant.Mcp.Tests.Infrastructure;

/// <summary>Hosts the real MCP server in memory with a test API key.</summary>
public class McpServerFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-mcp-api-key-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services => services.PostConfigure<McpSecurityOptions>(o => o.ApiKey = TestApiKey));
    }

    public PolicyIndex Index => Services.GetRequiredService<PolicyIndex>();

    /// <summary>Connects the official MCP C# client to the in-memory server over Streamable HTTP.</summary>
    public async Task<McpClient> CreateMcpClientAsync(string? apiKey = TestApiKey)
    {
        var httpClient = CreateClient();
        var headers = new Dictionary<string, string>();
        if (apiKey is not null)
        {
            headers[McpSecurityOptions.HeaderName] = apiKey;
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = headers,
            },
            httpClient,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }
}
