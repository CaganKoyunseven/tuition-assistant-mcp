using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using TuitionAssistant.Mcp.Indexing;
using TuitionAssistant.Mcp.Security;
using TuitionAssistant.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<McpSecurityOptions>()
    .Bind(builder.Configuration.GetSection("Mcp"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey) && o.ApiKey.Length >= 16, "Mcp:ApiKey is required (at least 16 characters).")
    .ValidateOnStart();
builder.Services.Configure<PolicyIndexOptions>(builder.Configuration.GetSection("Policy"));

// The index is a singleton: built once from the policy document, then only read.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<PolicyIndexOptions>>().Value;
    var path = Path.Combine(AppContext.BaseDirectory, options.DocumentPath);
    return new PolicyIndex(PolicyDocument.Load(path), options.MinScore);
});

// Official MCP C# SDK: Streamable HTTP transport in stateless mode (no session affinity needed).
builder.Services.AddMcpServer(options =>
        options.ServerInfo = new Implementation { Name = "tuition-policy-mcp", Version = "1.0.0" })
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<TuitionPolicyTools>();

var app = builder.Build();

// Build the index now, before the server starts accepting MCP requests (never per question).
var index = app.Services.GetRequiredService<PolicyIndex>();
app.Logger.LogInformation(
    "Indexed {Chunks} chunks from {DocumentId} (sha256 {Sha256}) in {Elapsed} ms",
    index.Chunks.Count, index.Document.DocumentId, index.Document.Sha256[..12], index.BuildDuration.TotalMilliseconds.ToString("0.0"));

app.UseMiddleware<ApiKeyMiddleware>();

// Liveness probe; also shows which document was indexed.
app.MapGet("/health", (PolicyIndex policyIndex) => Results.Ok(new
{
    status = "ok",
    service = "tuition-policy-mcp",
    documentId = policyIndex.Document.DocumentId,
    chunks = policyIndex.Chunks.Count,
    indexedAtUtc = policyIndex.IndexedAtUtc,
    documentSha256 = policyIndex.Document.Sha256,
}));

app.MapMcp("/mcp");

app.Run();
