using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Agent;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Endpoints;
using TuitionAssistant.Agent.Llm;
using TuitionAssistant.Agent.Orchestration;
using TuitionAssistant.Agent.Payments;
using TuitionAssistant.Agent.Security;
using TuitionAssistant.Agent.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection("Mcp"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

builder.Services.AddAgentAuthentication();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

// The LLM client is chosen by configuration: the real one, or an offline stand-in for demos and tests.
if (string.Equals(builder.Configuration["Llm:Provider"], "Fake", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ILlmClient, FakeLlmClient>();
}
else
{
    builder.Services.AddHttpClient<ILlmClient, OpenAiChatClient>(c => c.Timeout = TimeSpan.FromSeconds(90));
}

builder.Services.AddHttpClient<ITuitionGatewayClient, TuitionGatewayClient>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<IPolicySearchClient, McpPolicySearchClient>();
builder.Services.AddSingleton<PendingPaymentStore>();
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddSingleton<ITraceWriter, FileTraceWriter>();
builder.Services.AddTransient<GetMyTuitionTool>();
builder.Services.AddTransient<PreparePaymentTool>();
builder.Services.AddTransient<ToolRegistry>();
builder.Services.AddTransient<AgentOrchestrator>();
builder.Services.AddTransient<PaymentConfirmationService>();

var app = builder.Build();

// Expected failures become RFC 7807 problem responses with a stable "code" (same shape as the Tuition API).
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AgentException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = ex.Status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = ex.Status, Title = ex.Code, Detail = ex.Message, Extensions = { ["code"] = ex.Code } },
            options: null, contentType: "application/problem+json");
    }
});

// Chat UI (wwwroot) is public; every API call it makes carries the student's token.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Liveness probe used by Docker, Azure Container Apps and the smoke tests.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tuition-agent" })).AllowAnonymous();
app.MapAgentEndpoints();

app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
