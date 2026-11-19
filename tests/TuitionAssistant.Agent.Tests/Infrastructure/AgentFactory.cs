using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TuitionAssistant.Agent.Llm;
using TuitionAssistant.Agent.Orchestration;
using TuitionAssistant.Agent.Tools;

namespace TuitionAssistant.Agent.Tests.Infrastructure;

/// <summary>Agent under test with every external dependency (LLM, MCP, gateway) replaced by a controllable fake.</summary>
public sealed class AgentFactory : WebApplicationFactory<Program>
{
    private static readonly RSA Key = RSA.Create(2048);

    public ScriptedLlm Llm { get; } = new();

    public StubPolicyClient Policy { get; } = new();

    public StubGateway Gateway { get; } = new();

    public FakeClock Clock { get; } = new();

    public string DefaultModel { get; set; } = "test-model";

    public string AllowedModels { get; set; } = "other-model";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PublicKeyPem"] = Key.ExportSubjectPublicKeyInfoPem(),
            ["Llm:Model"] = DefaultModel,
            ["Llm:AllowedModels"] = AllowedModels,
            ["Llm:InputPricePerMillion"] = "1",
            ["Llm:OutputPricePerMillion"] = "2",
            ["Agent:TraceDirectory"] = "",
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILlmClient>();
            services.AddSingleton<ILlmClient>(Llm);
            services.RemoveAll<IPolicySearchClient>();
            services.AddSingleton<IPolicySearchClient>(Policy);
            services.RemoveAll<ITuitionGatewayClient>();
            services.AddSingleton<ITuitionGatewayClient>(Gateway);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public static string Token(string studentNo = "20230001", string role = "student", DateTime? expires = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "tuition-auth",
            Audience = "tuition-api",
            Expires = expires ?? DateTime.UtcNow.AddMinutes(30),
            Claims = new Dictionary<string, object> { ["sub"] = studentNo, ["role"] = role, ["student_no"] = studentNo },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(Key), SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public HttpClient ClientFor(string? token)
    {
        var client = CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public static async Task<JsonNode> Json(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    public static Task<HttpResponseMessage> Chat(HttpClient client, string sessionId, string message, string? model = null) =>
        client.PostAsJsonAsync("/api/v1/agent/chat", new { sessionId, message, model });
}

/// <summary>Replays prepared model responses and records what it was asked.</summary>
public sealed class ScriptedLlm : ILlmClient
{
    private readonly Queue<Func<IReadOnlyList<LlmMessage>, LlmResponse>> _script = new();

    public List<(string Model, IReadOnlyList<LlmMessage> Messages, IReadOnlyList<ToolDefinition> Tools)> Calls { get; } = [];

    public Exception? FailWith { get; set; }

    public ScriptedLlm Then(LlmResponse response)
    {
        _script.Enqueue(_ => response);
        return this;
    }

    public ScriptedLlm ThenCalls(params (string Name, string Args)[] calls) =>
        Then(new LlmResponse(null, calls.Select((c, i) => new ToolCall($"call_{i}", c.Name, c.Args)).ToList(), "tool_calls", new LlmUsage(100, 10)));

    public ScriptedLlm ThenAnswers(string text) => Then(new LlmResponse(text, [], "stop", new LlmUsage(200, 20)));

    public Task<LlmResponse> CompleteAsync(string model, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken)
    {
        Calls.Add((model, messages.ToList(), tools));
        if (FailWith is not null)
        {
            throw FailWith;
        }

        return Task.FromResult(_script.Count > 0 ? _script.Dequeue()(messages) : new LlmResponse("(script exhausted)", [], "stop", new LlmUsage(1, 1)));
    }
}

public sealed class StubPolicyClient : IPolicySearchClient
{
    public List<string> Calls { get; } = [];

    public bool Unavailable { get; set; }

    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken) =>
        Unavailable
            ? throw new HttpRequestException("mcp down")
            : Task.FromResult<IReadOnlyList<ToolDefinition>>([new ToolDefinition("search_tuition_policies", "Search policies", JsonNode.Parse("""{"type":"object","properties":{"query":{"type":"string"}}}""")!)]);

    public Task<McpCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        Calls.Add(arguments.GetRawText());
        return Task.FromResult(new McpCallResult("""{"matches":[{"section":"3.1","text":"Installments need at least $300."}]}""", false));
    }
}

public sealed class StubGateway : ITuitionGatewayClient
{
    public decimal Balance { get; set; } = 3000m;

    public List<(string StudentNo, string? Term, string Token)> Queries { get; } = [];

    public List<(string StudentNo, string Term, decimal Amount, string TransactionId, string Token)> Payments { get; } = [];

    /// <summary>When set, the next Pay calls return these (e.g. a 422 body) instead of a 201.</summary>
    public Queue<Func<GatewayResponse>> PayScript { get; } = new();

    public int QueryStatus { get; set; } = 200;

    public Task<GatewayResponse> QueryTuitionAsync(string studentNo, string? term, string bearerToken, CancellationToken cancellationToken)
    {
        Queries.Add((studentNo, term, bearerToken));
        if (QueryStatus != 200)
        {
            return Task.FromResult(new GatewayResponse(QueryStatus, """{"title":"Not found","detail":"No tuition record for that term."}"""));
        }

        return Task.FromResult(new GatewayResponse(200, JsonSerializer.Serialize(new { studentNo, scope = term is null ? "OVERALL" : "TERM", term, tuitionTotal = 6000m, paidAmount = 6000m - Balance, balance = Balance, status = "PARTIALLY_PAID" })));
    }

    public Task<GatewayResponse> PayAsync(string studentNo, string term, decimal amount, string transactionId, string bearerToken, CancellationToken cancellationToken)
    {
        Payments.Add((studentNo, term, amount, transactionId, bearerToken));
        if (PayScript.Count > 0)
        {
            return Task.FromResult(PayScript.Dequeue()());
        }

        Balance -= amount;
        return Task.FromResult(new GatewayResponse(201, JsonSerializer.Serialize(new { status = "SUCCESS", transactionId, studentNo, term, paidAmount = amount, remainingBalance = Balance })));
    }
}

public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
