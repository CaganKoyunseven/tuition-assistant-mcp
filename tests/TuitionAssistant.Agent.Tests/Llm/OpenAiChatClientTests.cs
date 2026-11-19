using System.Text.Json.Nodes;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Llm;

namespace TuitionAssistant.Agent.Tests.Llm;

public class OpenAiChatClientTests
{
    private static readonly ToolDefinition Tool = new("get_my_tuition", "desc", JsonNode.Parse("""{"type":"object","properties":{}}""")!);

    [Fact]
    public void BuildRequest_UsesTheGivenModel_AndOmitsOptionalSettings()
    {
        var body = OpenAiChatClient.BuildRequest(new LlmOptions(), "my-model", [new LlmMessage("user", "hi")], [Tool]);

        Assert.Equal("my-model", body["model"]!.GetValue<string>());
        Assert.Null(body["temperature"]);
        Assert.Null(body["reasoning_effort"]);
        Assert.Equal("auto", body["tool_choice"]!.GetValue<string>());
        Assert.Equal("get_my_tuition", body["tools"]![0]!["function"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void BuildRequest_SendsTemperatureAndReasoningEffortOnlyWhenConfigured()
    {
        var settings = new LlmOptions { Temperature = 0, ReasoningEffort = "low" };

        var body = OpenAiChatClient.BuildRequest(settings, "m", [new LlmMessage("user", "hi")], []);

        Assert.Equal(0, body["temperature"]!.GetValue<double>());
        Assert.Equal("low", body["reasoning_effort"]!.GetValue<string>());
        Assert.Null(body["tools"]);
    }

    [Fact]
    public void BuildRequest_ToolMessagesCarryTheirCallId()
    {
        var calls = new[] { new ToolCall("c1", "get_my_tuition", "{}") };

        var body = OpenAiChatClient.BuildRequest(new LlmOptions(), "m",
            [new LlmMessage("assistant", null, calls), new LlmMessage("tool", "{\"x\":1}", ToolCallId: "c1")], []);

        Assert.Equal("c1", body["messages"]![0]!["tool_calls"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("c1", body["messages"]![1]!["tool_call_id"]!.GetValue<string>());
    }

    [Fact]
    public void ParseResponse_ReadsTextAndUsage()
    {
        var response = OpenAiChatClient.ParseResponse("""
            {"choices":[{"message":{"content":"Hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":12,"completion_tokens":3}}
            """);

        Assert.Equal("Hello", response.Text);
        Assert.Empty(response.ToolCalls);
        Assert.Equal(new LlmUsage(12, 3), response.Usage);
    }

    [Fact]
    public void ParseResponse_ReadsToolCalls()
    {
        var response = OpenAiChatClient.ParseResponse("""
            {"choices":[{"message":{"content":null,"tool_calls":[{"id":"c1","type":"function","function":{"name":"get_my_tuition","arguments":"{\"term\":\"2026-FALL\"}"}}]},"finish_reason":"tool_calls"}]}
            """);

        var call = Assert.Single(response.ToolCalls);
        Assert.Equal(("c1", "get_my_tuition", """{"term":"2026-FALL"}"""), (call.Id, call.Name, call.ArgumentsJson));
        Assert.Equal(0, response.Usage.InputTokens);
    }

    [Fact]
    public void ParseResponse_WithoutChoices_Throws() =>
        Assert.Throws<LlmException>(() => OpenAiChatClient.ParseResponse("""{"choices":[]}"""));

    [Fact]
    public async Task CompleteAsync_WithoutApiKey_ThrowsHelpfulError()
    {
        var client = new OpenAiChatClient(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new LlmOptions { ApiKey = "" }));

        var ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync("m", [], [], CancellationToken.None));

        Assert.Contains("API key", ex.Message);
    }

    [Fact]
    public void AvailableModels_DefaultFirst_NoDuplicates()
    {
        var settings = new LlmOptions { Model = "a", AllowedModels = "b,a, c" };

        Assert.Equal(new[] { "a", "b", "c" }, settings.AvailableModels());
    }
}
