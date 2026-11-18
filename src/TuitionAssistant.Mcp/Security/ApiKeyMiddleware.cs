using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TuitionAssistant.Mcp.Security;

/// <summary>MCP security settings (configuration section <c>Mcp</c>).</summary>
public sealed class McpSecurityOptions
{
    public const string HeaderName = "X-Mcp-Api-Key";

    /// <summary>Shared secret the agent sends in <c>X-Mcp-Api-Key</c>. Env: <c>Mcp__ApiKey</c>.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Protects the MCP endpoint with a shared API key (the MCP path does not go through the API gateway).
/// <c>/health</c> stays open. The comparison is constant-time so the key cannot be guessed byte by byte.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IOptions<McpSecurityOptions> options)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(options.Value.ApiKey ?? string.Empty);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/mcp") && !IsAuthorized(context.Request.Headers[McpSecurityOptions.HeaderName]))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "unauthorized",
                message = $"A valid {McpSecurityOptions.HeaderName} header is required.",
            });
            return;
        }

        await next(context);
    }

    private bool IsAuthorized(string? provided) =>
        provided is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expected);
}
