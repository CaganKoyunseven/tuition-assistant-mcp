using System.Net;
using System.Text.Json;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests;

public class HealthEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    [Fact]
    public async Task Health_WhenServiceIsRunning_ReturnsOkWithServiceName()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("tuition-api", body.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
