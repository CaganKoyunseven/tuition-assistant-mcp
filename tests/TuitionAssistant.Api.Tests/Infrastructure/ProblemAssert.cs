using System.Net;
using System.Text.Json;

namespace TuitionAssistant.Api.Tests.Infrastructure;

/// <summary>Assertions for RFC 7807 ProblemDetails responses.</summary>
public static class ProblemAssert
{
    /// <summary>Asserts status, content type and <c>code</c>; returns the parsed body for further checks.</summary>
    public static async Task<JsonElement> HasCode(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Expected {(int)status} but got {(int)response.StatusCode}: {raw}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal($"https://httpstatuses.io/{(int)status}", body.GetProperty("type").GetString());
        return body;
    }
}
