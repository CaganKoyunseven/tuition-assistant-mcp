using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using TuitionAssistant.Agent.Tools;
using TuitionAssistant.Agent.Tests.Infrastructure;

namespace TuitionAssistant.Agent.Tests.Payments;

public class PaymentConfirmationTests
{
    private static readonly string Session = Guid.NewGuid().ToString();

    private static async Task<(Guid PendingId, JsonNode Body)> Prepare(AgentFactory factory, HttpClient client, string amount = "1000", string term = "2026-FALL")
    {
        factory.Llm.ThenCalls(("prepare_payment", $$"""{"term":"{{term}}","amount":{{amount}}}""")).ThenAnswers("Please press Confirm.");
        var body = await AgentFactory.Json(await AgentFactory.Chat(client, Session, $"pay {amount} for {term}"));
        var pending = body["pendingPayment"];
        return (pending is null ? Guid.Empty : Guid.Parse(pending["pendingId"]!.GetValue<string>()), body);
    }

    private static Task<HttpResponseMessage> Confirm(HttpClient client, Guid id, string? session = null) =>
        client.PostAsJsonAsync($"/api/v1/agent/payments/{id}/confirm", new { sessionId = session ?? Session });

    [Fact]
    public async Task PreparePayment_ReturnsPendingSummary_AndNeverCallsThePayApi()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());

        var (id, body) = await Prepare(factory, client);

        Assert.NotEqual(Guid.Empty, id);
        var pending = body["pendingPayment"]!;
        Assert.Equal("AWAITING_USER_CONFIRMATION", pending["status"]!.GetValue<string>());
        Assert.Equal(1000m, pending["amount"]!.GetValue<decimal>());
        Assert.Equal(3000m, pending["currentBalance"]!.GetValue<decimal>());
        Assert.Equal(2000m, pending["remainingAfterPayment"]!.GetValue<decimal>());
        Assert.Empty(factory.Gateway.Payments);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("10.555")]
    [InlineData("3000.01")]
    public async Task PreparePayment_InvalidAmount_CreatesNoPendingPayment(string amount)
    {
        using var factory = new AgentFactory();

        var (id, _) = await Prepare(factory, factory.ClientFor(AgentFactory.Token()), amount);

        Assert.Equal(Guid.Empty, id);
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task PreparePayment_TermWithoutBalance_CreatesNoPendingPayment()
    {
        using var factory = new AgentFactory();
        factory.Gateway.Balance = 0m;

        var (id, _) = await Prepare(factory, factory.ClientFor(AgentFactory.Token()), "100");

        Assert.Equal(Guid.Empty, id);
        Assert.Contains("NOTHING_TO_PAY", factory.Llm.Calls[1].Messages.Last(m => m.Role == "tool").Content);
    }

    [Fact]
    public async Task Confirm_PaysOnceWithAgentChannelAndUsersToken()
    {
        using var factory = new AgentFactory();
        var token = AgentFactory.Token();
        var client = factory.ClientFor(token);
        var (id, _) = await Prepare(factory, client);

        var response = await Confirm(client, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await AgentFactory.Json(response);
        Assert.Equal("SUCCESS", body["status"]!.GetValue<string>());
        Assert.Contains("Remaining balance: $2000.00", body["answer"]!.GetValue<string>());
        var payment = Assert.Single(factory.Gateway.Payments);
        Assert.Equal(("20230001", "2026-FALL", 1000m), (payment.StudentNo, payment.Term, payment.Amount));
        Assert.StartsWith("AGT-", payment.TransactionId);
        Assert.Equal(token, payment.Token);
        var kinds = body["trace"]!["steps"]!.AsArray().Select(s => s!["kind"]!.GetValue<string>());
        Assert.Equal(new[] { "confirmation", "rest" }, kinds);
    }

    [Fact]
    public async Task Confirm_Twice_SecondCallReturns409AndPaysOnlyOnce()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);

        await Confirm(client, id);
        var second = await Confirm(client, id);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("PENDING_PAYMENT_ALREADY_USED", (await AgentFactory.Json(second))["code"]!.GetValue<string>());
        Assert.Single(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Confirm_ConcurrentDoubleClick_PaysExactlyOnce()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Confirm(client, id)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Confirm_AfterExpiry_Returns410AndDoesNotPay()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);
        factory.Clock.Advance(TimeSpan.FromSeconds(301));

        var response = await Confirm(client, id);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("PENDING_PAYMENT_EXPIRED", (await AgentFactory.Json(response))["code"]!.GetValue<string>());
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Confirm_UnknownId_Returns404()
    {
        using var factory = new AgentFactory();

        var response = await Confirm(factory.ClientFor(AgentFactory.Token()), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_ByAnotherStudent_Returns403AndDoesNotPay()
    {
        using var factory = new AgentFactory();
        var (id, _) = await Prepare(factory, factory.ClientFor(AgentFactory.Token("20230001")));

        var response = await Confirm(factory.ClientFor(AgentFactory.Token("20230002")), id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Confirm_WithOtherSession_Returns403()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);

        var response = await Confirm(client, id, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Confirm_WithoutToken_Returns401()
    {
        using var factory = new AgentFactory();

        var response = await Confirm(factory.ClientFor(null), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_WhenPayApiRejects_ReportsFailureAndNeverClaimsSuccess()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);
        factory.Gateway.PayScript.Enqueue(() => new GatewayResponse(422, """{"code":"AMOUNT_EXCEEDS_BALANCE","detail":"Amount exceeds the outstanding balance."}"""));

        var body = await AgentFactory.Json(await Confirm(client, id));

        Assert.Equal("FAILED", body["status"]!.GetValue<string>());
        Assert.Contains("not recorded", body["answer"]!.GetValue<string>());
        Assert.Contains("exceeds", body["answer"]!.GetValue<string>());
    }

    [Fact]
    public async Task Confirm_TransientFailure_RetriesOnceWithSameTransactionId()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);
        factory.Gateway.PayScript.Enqueue(() => throw new HttpRequestException("connection reset"));

        var body = await AgentFactory.Json(await Confirm(client, id));

        Assert.Equal("SUCCESS", body["status"]!.GetValue<string>());
        Assert.Equal(2, factory.Gateway.Payments.Count);
        Assert.Equal(factory.Gateway.Payments[0].TransactionId, factory.Gateway.Payments[1].TransactionId);
    }

    [Fact]
    public async Task Confirm_RetryHitsDuplicateOfOurOwnPayment_IsTreatedAsSuccess()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);
        factory.Gateway.PayScript.Enqueue(() => throw new HttpRequestException("timeout after the API committed"));
        factory.Gateway.PayScript.Enqueue(() => new GatewayResponse(409,
            """{"code":"DUPLICATE_TRANSACTION","original":{"studentNo":"20230001","term":"2026-FALL","amount":1000.00}}"""));

        var body = await AgentFactory.Json(await Confirm(client, id));

        Assert.Equal("SUCCESS", body["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Confirm_GatewayDownTwice_Returns502()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);
        factory.Gateway.PayScript.Enqueue(() => throw new HttpRequestException("down"));
        factory.Gateway.PayScript.Enqueue(() => throw new HttpRequestException("down"));

        var response = await Confirm(client, id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_PreventsLaterConfirm()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);

        var cancel = await client.PostAsJsonAsync($"/api/v1/agent/payments/{id}/cancel", new { sessionId = Session });
        var confirm = await Confirm(client, id);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Empty(factory.Gateway.Payments);
    }

    [Fact]
    public async Task Confirm_RecordsOutcomeInHistoryForLaterTurns()
    {
        using var factory = new AgentFactory();
        var client = factory.ClientFor(AgentFactory.Token());
        var (id, _) = await Prepare(factory, client);
        await Confirm(client, id);
        factory.Llm.ThenAnswers("Yes, it went through.");

        await AgentFactory.Chat(client, Session, "did it work?");

        Assert.Contains(factory.Llm.Calls.Last().Messages, m => m.Role == "system" && m.Content!.Contains("Payment of $1000.00"));
    }
}
