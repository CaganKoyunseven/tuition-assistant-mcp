using System.Net;
using System.Text.Json;
using TuitionAssistant.Agent.Tests.Infrastructure;

namespace TuitionAssistant.Agent.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_WhenServiceIsRunning_ReturnsOkWithServiceName()
    {
        using var factory = new AgentFactory();

        var response = await factory.ClientFor(null).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("tuition-agent", body.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        using var factory = new AgentFactory();

        var response = await factory.ClientFor(null).GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
