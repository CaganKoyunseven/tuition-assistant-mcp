namespace TuitionAssistant.Agent.Configuration;

/// <summary>LLM settings (section <c>Llm</c>). The model and the key are always configuration, never code.</summary>
public class LlmOptions
{
    /// <summary><c>OpenAI</c> (any OpenAI-compatible Chat Completions endpoint) or <c>Fake</c> (offline demo and tests).</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>Base URL of the Chat Completions API. Change it to use another OpenAI-compatible provider.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Default model. Env: <c>Llm__Model</c>.</summary>
    public string Model { get; set; } = "";

    /// <summary>Provider API key. Env: <c>Llm__ApiKey</c>. Never logged or traced.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Extra models a caller may pick per request (comma separated). Empty: only <see cref="Model"/>.</summary>
    public string AllowedModels { get; set; } = "";

    /// <summary>Omitted from the request when null (some models reject a custom temperature).</summary>
    public double? Temperature { get; set; }

    /// <summary>Only sent when set (reasoning models).</summary>
    public string? ReasoningEffort { get; set; }

    public int MaxToolIterations { get; set; } = 5;

    public decimal InputPricePerMillion { get; set; }

    public decimal OutputPricePerMillion { get; set; }

    /// <summary>The default model first, then the extra allowed models, without duplicates.</summary>
    public IReadOnlyList<string> AvailableModels() =>
        new[] { Model }
            .Concat(AllowedModels.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

/// <summary>Tuition API reached through the Kong gateway (section <c>Gateway</c>).</summary>
public class GatewayOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
}

/// <summary>MCP policy server (section <c>Mcp</c>).</summary>
public class McpOptions
{
    public string Url { get; set; } = "http://localhost:8081/mcp";

    public string? ApiKey { get; set; }
}

/// <summary>Token validation (section <c>Jwt</c>). The agent only holds the public key.</summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "tuition-auth";

    public string Audience { get; set; } = "tuition-api";

    public string? PublicKeyPem { get; set; }
}

/// <summary>Agent behaviour (section <c>Agent</c>).</summary>
public class AgentOptions
{
    public int PendingPaymentTtlSeconds { get; set; } = 300;

    public int HistoryMaxMessages { get; set; } = 20;

    public int SessionIdleMinutes { get; set; } = 30;

    /// <summary>Folder for raw traces; empty disables writing.</summary>
    public string TraceDirectory { get; set; } = "traces";
}
