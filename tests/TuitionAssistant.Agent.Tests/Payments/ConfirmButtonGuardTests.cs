using System.Text.Json.Nodes;
using TuitionAssistant.Agent.Tests.Infrastructure;

namespace TuitionAssistant.Agent.Tests.Payments;

/// <summary>
/// Regression tests: in a longer chat the model sometimes copied the wording of an earlier payment reply
/// ("I prepared your payment, press Confirm") without calling prepare_payment, so no Confirm card was shown.
/// </summary>
public class ConfirmButtonGuardTests
{
    private const string FakeClaim = "I have prepared a payment of $100 for 2026-FALL. Please press the Confirm button to authorize it.";

    private static Task<HttpResponseMessage> Chat(AgentFactory factory, string message) =>
        AgentFactory.Chat(factory.ClientFor(AgentFactory.Token()), Guid.NewGuid().ToString(), message);

    [Fact]
    public async Task ClaimWithoutPrepare_IsCorrected_AndTheModelPreparesThePayment()
    {
        using var factory = new AgentFactory();
        factory.Llm
            .ThenAnswers(FakeClaim)
            .ThenCalls(("prepare_payment", """{"term":"2026-FALL","amount":100}"""))
            .ThenAnswers("Prepared. Please press Confirm.");

        var body = await AgentFactory.Json(await Chat(factory, "Pay $100 of my 2026-FALL balance"));

        Assert.NotNull(body["pendingPayment"]);
        Assert.Equal("Prepared. Please press Confirm.", body["answer"]!.GetValue<string>());
        Assert.Contains(factory.Llm.Calls[1].Messages, m => m.Role == "system" && m.Content!.Contains("prepare_payment was NOT called"));
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task ClaimRepeatedAfterCorrection_IsReplacedByAnHonestMessage_AndNoPendingPayment()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers(FakeClaim).ThenAnswers(FakeClaim);

        var body = await AgentFactory.Json(await Chat(factory, "Pay $100 of my 2026-FALL balance"));

        Assert.Null(body["pendingPayment"]);
        Assert.Contains("nothing to confirm", body["answer"]!.GetValue<string>());
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task NormalPolicyAnswer_IsNotTouchedByTheGuard()
    {
        using var factory = new AgentFactory();
        factory.Llm.ThenAnswers("The Fall deadline is September 30, 2026 (Policy §2.1).");

        var body = await AgentFactory.Json(await Chat(factory, "When is the deadline?"));

        Assert.Single(factory.Llm.Calls);
        Assert.Equal("The Fall deadline is September 30, 2026 (Policy §2.1).", body["answer"]!.GetValue<string>());
    }
}
