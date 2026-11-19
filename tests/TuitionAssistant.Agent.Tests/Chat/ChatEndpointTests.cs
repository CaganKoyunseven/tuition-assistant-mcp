using System.Net;
using TuitionAssistant.Agent.Tests.Infrastructure;

namespace TuitionAssistant.Agent.Tests.Chat;

public class ChatEndpointTests
{
    private static readonly string Session = Guid.NewGuid().ToString();

    [Fact]
    public async Task Chat_WithoutToken_Returns401()
    {
        using var factory = new AgentFactory();

        var response = await AgentFactory.Chat(factory.ClientFor(null), Session, "hi");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WithBankToken_Returns403()
    {
        using var factory = new AgentFactory();

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token(role: "bank")), Session, "hi");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WithExpiredToken_Returns401()
    {
        using var factory = new AgentFactory();
        var expired = AgentFactory.Token(expires: DateTime.UtcNow.AddMinutes(-5));

        var response = await AgentFactory.Chat(factory.ClientFor(expired), Session, "hi");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Chat_WithEmptyMessage_Returns400(string message)
    {
        using var factory = new AgentFactory();

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await AgentFactory.Json(response))["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_WithNonGuidSession_Returns400()
    {
        using var factory = new AgentFactory();

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), "not-a-guid", "hi");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Chat_PolicyQuestion_CallsMcpToolAndReturnsTrace()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenCalls(("search_tuition_policies", """{"query":"installment rules"}""")).ThenAnswers("Up to three installments (Policy §3.2).");

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "How do installments work?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await AgentFactory.Json(response);
        Assert.Equal("Up to three installments (Policy §3.2).", body["answer"]!.GetValue<string>());
        Assert.Single(factory.Policy.Calls);
        var steps = body["trace"]!["steps"]!.AsArray();
        Assert.Equal(new[] { "llm", "mcp", "llm" }, steps.Select(s => s!["kind"]!.GetValue<string>()));
        Assert.Equal("tool_selection", steps[0]!["name"]!.GetValue<string>());
        Assert.Equal("final_answer", steps[2]!["name"]!.GetValue<string>());
        Assert.Empty(factory.Gateway.Queries);

        // The MCP result is summarised (section + heading + excerpt), not cut in the middle of the JSON.
        var match = steps[1]!["output"]!["matches"]![0]!;
        Assert.Equal("3.1", match["section"]!.GetValue<string>());
        Assert.Contains("Installments", match["excerpt"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_AccountQuestion_UsesTokenStudentNumberAndBearerToken()
    {
        using var factory = new AgentFactory();
        var token = AgentFactory.Token("20230042");
        // The model tries to smuggle in another student number; the tool has no such parameter and ignores it.
        factory.Llm.ThenCalls(("get_my_tuition", """{"studentNo":"99999999","term":"2026-FALL"}""")).ThenAnswers("Your balance is $3,000.00.");

        var response = await AgentFactory.Chat(factory.ClientFor(token), Session, "What do I owe?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(factory.Gateway.Queries);
        Assert.Equal("20230042", query.StudentNo);
        Assert.Equal("2026-FALL", query.Term);
        Assert.Equal(token, query.Token);
    }

    [Fact]
    public async Task Chat_CombinedQuestion_RunsBothToolsAndSumsTokens()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenCalls(("get_my_tuition", "{}"), ("search_tuition_policies", """{"query":"installments"}""")).ThenAnswers("done");

        var body = await AgentFactory.Json(await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "Can I pay my balance in installments?"));

        var kinds = body["trace"]!["steps"]!.AsArray().Select(s => s!["kind"]!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "llm", "rest", "mcp", "llm" }, kinds);
        var totals = body["trace"]!["totals"]!;
        Assert.Equal(300, totals["inputTokens"]!.GetValue<int>());
        Assert.Equal(30, totals["outputTokens"]!.GetValue<int>());
        Assert.Equal(0.00036m, totals["estimatedCostUsd"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Chat_InvalidTermFromModel_IsReturnedToModelAsToolError()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenCalls(("get_my_tuition", """{"term":"fall 2026"}""")).ThenAnswers("Please give a term like 2026-FALL.");

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "balance for fall 2026");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.Gateway.Queries);
        var toolMessage = factory.Llm.Calls[1].Messages.Last(m => m.Role == "tool");
        Assert.Contains("INVALID_TERM", toolMessage.Content);
    }

    [Fact]
    public async Task Chat_ApiReturns404_ModelReceivesStructuredError()
    {
        using var factory = new AgentFactory();
        factory.Gateway.QueryStatus = 404;
        factory.Llm.ThenCalls(("get_my_tuition", """{"term":"2030-FALL"}""")).ThenAnswers("No record for that term.");

        var body = await AgentFactory.Json(await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "balance 2030-FALL"));

        var toolMessage = factory.Llm.Calls[1].Messages.Last(m => m.Role == "tool");
        Assert.Contains("\"httpStatus\":404", toolMessage.Content);
        var restStep = body["trace"]!["steps"]!.AsArray().Single(s => s!["kind"]!.GetValue<string>() == "rest");
        Assert.Equal(404, restStep!["httpStatus"]!.GetValue<int>());
        Assert.Equal("ERROR", restStep["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_UnknownToolFromModel_ReturnsToolErrorInsteadOfCrashing()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenCalls(("pay_tuition", "{}")).ThenAnswers("I cannot pay directly.");

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "pay now");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("UNKNOWN_TOOL", factory.Llm.Calls[1].Messages.Last(m => m.Role == "tool").Content);
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Chat_ModelNeverStopsCallingTools_StopsAtLimitWithFallbackAnswer()
    {
        using var factory = new AgentFactory();
        for (var i = 0; i < 20; i++)
        {
            factory.Llm.ThenCalls(("search_tuition_policies", """{"query":"loop"}"""));
        }

        var body = await AgentFactory.Json(await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "loop"));

        Assert.Equal("LIMIT_REACHED", body["trace"]!["outcome"]!.GetValue<string>());
        Assert.Equal(5, factory.Llm.Calls.Count);
        Assert.Contains("could not complete", body["answer"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_LlmFails_Returns502UpstreamError()
    {
        using var factory = new AgentFactory();
        factory.Llm.FailWith = new TuitionAssistant.Agent.Llm.LlmException("The LLM service could not be reached.");

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "hi");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("UPSTREAM_ERROR", (await AgentFactory.Json(response))["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_McpServerDown_Returns502()
    {
        using var factory = new AgentFactory();
        factory.Policy.Unavailable = true;

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Session, "hi");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Chat_SecondTurn_SendsHistoryToModel()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers("first answer").ThenAnswers("second answer");
        var client = factory.ClientFor(AgentFactory.Token());

        await AgentFactory.Chat(client, Session, "first question");
        await AgentFactory.Chat(client, Session, "second question");

        var secondCall = factory.Llm.Calls[1].Messages;
        Assert.Contains(secondCall, m => m.Role == "user" && m.Content == "first question");
        Assert.Contains(secondCall, m => m.Role == "assistant" && m.Content == "first answer");
    }

    [Fact]
    public async Task Chat_SessionOfAnotherStudent_Returns403()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers("ok");
        await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token("20230001")), Session, "hello");

        var response = await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token("20230002")), Session, "hello");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chat_SystemPromptNamesTheSignedInStudentAndDate()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers("ok");

        await AgentFactory.Chat(factory.ClientFor(AgentFactory.Token("20230042")), Session, "hello");

        var system = factory.Llm.Calls[0].Messages[0];
        Assert.Equal("system", system.Role);
        Assert.Contains("20230042", system.Content);
        Assert.Contains("2026-11-19", system.Content);
    }
}
