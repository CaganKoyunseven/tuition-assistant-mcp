using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;

namespace TuitionAssistant.Agent.Tools;

/// <summary>Raw HTTP result from the Tuition API (through the gateway).</summary>
public sealed record GatewayResponse(int StatusCode, string Body);

/// <summary>Calls the Tuition REST API through Kong, always with the end user's own bearer token.</summary>
public interface ITuitionGatewayClient
{
    Task<GatewayResponse> QueryTuitionAsync(string studentNo, string? term, string bearerToken, CancellationToken cancellationToken);

    Task<GatewayResponse> PayAsync(string studentNo, string term, decimal amount, string transactionId, string bearerToken, CancellationToken cancellationToken);
}

public sealed class TuitionGatewayClient(HttpClient http, IOptions<GatewayOptions> options) : ITuitionGatewayClient
{
    private string BaseUrl => options.Value.BaseUrl.TrimEnd('/');

    public async Task<GatewayResponse> QueryTuitionAsync(string studentNo, string? term, string bearerToken, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/api/v1/banking/tuition/{Uri.EscapeDataString(studentNo)}" + (term is null ? "" : $"?term={Uri.EscapeDataString(term)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return await SendAsync(request, cancellationToken);
    }

    public async Task<GatewayResponse> PayAsync(string studentNo, string term, decimal amount, string transactionId, string bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/banking/payments")
        {
            Content = JsonContent.Create(new { studentNo, term, amount, transactionId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Add("X-Client-Channel", "ai-agent");
        return await SendAsync(request, cancellationToken);
    }

    private async Task<GatewayResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken);
        return new GatewayResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
