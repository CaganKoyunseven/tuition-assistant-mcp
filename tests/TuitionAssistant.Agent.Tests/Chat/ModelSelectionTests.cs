using System.Net;
using TuitionAssistant.Agent.Tests.Infrastructure;

namespace TuitionAssistant.Agent.Tests.Chat;

public class ModelSelectionTests
{
    private static readonly string Session = Guid.NewGuid().ToString();

    [Fact]
    public async Task Models_ListsDefaultFirstThenAllowedModels()
    {
        using var factory = new AgentFactory { AllowedModels = "gpt-a, gpt-b ; test-model" };

        var response = await factory.ClientFor(AgentFactory.Token()).GetAsync("/api/v1/agent/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await AgentFactory.Json(response);
        Assert.Equal("test-model", body["defaultModel"]!.GetValue<string>());
        Assert.Equal(new[] { "test-model", "gpt-a", "gpt-b" }, body["models"]!.AsArray().Select(m => m!.GetValue<string>()));
    }

    [Fact]
    public async Task Models_WithoutToken_Returns401()
    {
        using var factory = new AgentFactory();

        var response = await factory.ClientFor(null).GetAsync("/api/v1/agent/models");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WithoutModel_UsesConfiguredDefault()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers("ok");

        var body = await AgentFactory.Json(await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "hi"));

        Assert.Equal("test-model", factory.Llm.Calls[0].Model);
        Assert.Equal("test-model", body["model"]!.GetValue<string>());
        Assert.Equal("test-model", body["trace"]!["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_WithAllowedModel_UsesIt()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers("ok");

        var body = await AgentFactory.Json(await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "hi", "other-model"));

        Assert.Equal("other-model", factory.Llm.Calls[0].Model);
        Assert.Equal("other-model", body["trace"]!["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_WithModelOutsideAllowList_Returns400AndDoesNotCallTheLlm()
    {
        using var factory = new AgentFactory();

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "hi", "expensive-model");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MODEL_NOT_ALLOWED", (await AgentFactory.Json(response))["code"]!.GetValue<string>());
        Assert.Empty(factory.Llm.Calls);
    }
}
