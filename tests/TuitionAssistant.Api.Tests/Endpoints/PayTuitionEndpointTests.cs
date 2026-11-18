using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

/// <summary>Each test uses its own seeded record so the tests are independent.</summary>
public class PayTuitionEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private const string Url = "/api/v1/banking/payments";

    private Task<HttpResponseMessage> PayAsync(string token, object body) =>
        factory.CreateClientWithToken(token).PostAsJsonAsync(Url, body);

    private async Task<long> PaidCentsAsync(string studentNo, string term = "2026-FALL")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TuitionDbContext>();
        return (await db.TuitionRecords.SingleAsync(r => r.StudentNo == studentNo && r.Term == term)).PaidCents;
    }

    [Fact]
    public async Task Pay_PartialPayment_Returns201AndDecreasesBalance()
    {
        // 20239001: total 1,137.00, nothing paid.
        var response = await PayAsync(factory.BankToken(), new { studentNo = "20239001", term = "2026-FALL", amount = 137.50m, transactionId = "T-PARTIAL-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("SUCCESS", body.GetProperty("status").GetString());
        Assert.Equal(137.50m, body.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(137.50m, body.GetProperty("totalPaid").GetDecimal());
        Assert.Equal(999.50m, body.GetProperty("remainingBalance").GetDecimal());
        Assert.Equal("PARTIALLY_PAID", body.GetProperty("recordStatus").GetString());
        Assert.Equal("BANK_APP", body.GetProperty("channel").GetString());
        Assert.Equal(13_750, await PaidCentsAsync("20239001"));
    }

    [Fact]
    public async Task Pay_FullPayment_Returns201WithZeroBalanceAndPaidStatus()
    {
        // 20239002: total 1,274.00.
        var response = await PayAsync(factory.BankToken(), new { studentNo = "20239002", term = "2026-FALL", amount = 1274.00m, transactionId = "T-FULL-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(0.00m, body.GetProperty("remainingBalance").GetDecimal());
        Assert.Equal("PAID", body.GetProperty("recordStatus").GetString());
    }

    [Fact]
    public async Task Pay_DuplicateTransactionId_Returns409WithOriginal_AndBalanceUnchanged()
    {
        var first = await PayAsync(factory.BankToken(), new { studentNo = "20239003", term = "2026-FALL", amount = 100m, transactionId = "T-DUP-1" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PayAsync(factory.BankToken(), new { studentNo = "20239003", term = "2026-FALL", amount = 100m, transactionId = "T-DUP-1" });

        var body = await ProblemAssert.HasCode(second, HttpStatusCode.Conflict, ErrorCodes.DuplicateTransaction);
        Assert.Equal("20239003", body.GetProperty("original").GetProperty("studentNo").GetString());
        Assert.Equal(100.00m, body.GetProperty("original").GetProperty("amount").GetDecimal());
        Assert.Equal(10_000, await PaidCentsAsync("20239003"));
    }

    [Fact]
    public async Task Pay_DuplicateOfAnotherStudentsTransaction_DoesNotRevealOriginal()
    {
        // SEED-TXN-0001 belongs to 20230001; student 20230004 tries to reuse it for their own record.
        var response = await PayAsync(factory.StudentToken("20230004"), new { studentNo = "20230004", term = "2026-FALL", amount = 10m, transactionId = "SEED-TXN-0001" });

        var body = await ProblemAssert.HasCode(response, HttpStatusCode.Conflict, ErrorCodes.DuplicateTransaction);
        Assert.False(body.TryGetProperty("original", out _));
    }

    [Fact]
    public async Task Pay_AmountExceedsBalance_Returns422WithCurrentBalance()
    {
        // 20230003: balance 250.00.
        var response = await PayAsync(factory.BankToken(), new { studentNo = "20230003", term = "2026-FALL", amount = 300m, transactionId = "T-EXCEED-1" });

        var body = await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, ErrorCodes.AmountExceedsBalance);
        Assert.Equal(250.00m, body.GetProperty("currentBalance").GetDecimal());
        Assert.Equal(575_000, await PaidCentsAsync("20230003"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("10.001")]
    [InlineData("1000000.01")]
    public async Task Pay_InvalidAmount_Returns400(string amount)
    {
        var json = $$"""{"studentNo":"20230001","term":"2026-FALL","amount":{{amount}},"transactionId":"T-BAD-AMOUNT"}""";
        var client = factory.CreateClientWithToken(factory.BankToken());

        var response = await client.PostAsync(Url, new StringContent(json, Encoding.UTF8, "application/json"));

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.InvalidAmount);
    }

    [Fact]
    public async Task Pay_InvalidTransactionId_Returns400()
    {
        var response = await PayAsync(factory.BankToken(), new { studentNo = "20230001", term = "2026-FALL", amount = 1m, transactionId = "has spaces!" });

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.InvalidTransactionId);
    }

    [Fact]
    public async Task Pay_AlreadyFullyPaidTerm_Returns422AlreadyPaid()
    {
        // 20230002 / 2026-FALL is fully paid in the seed data.
        var response = await PayAsync(factory.BankToken(), new { studentNo = "20230002", term = "2026-FALL", amount = 1m, transactionId = "T-PAID-1" });

        await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyPaid);
    }

    [Fact]
    public async Task Pay_StudentPayingForAnotherStudent_Returns403()
    {
        var response = await PayAsync(factory.StudentToken("20230001"), new { studentNo = "20230002", term = "2027-SPRING", amount = 1m, transactionId = "T-OTHER-1" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Forbidden, ErrorCodes.ForbiddenNotOwner);
        Assert.Equal(0, await PaidCentsAsync("20230002", "2027-SPRING"));
    }

    [Fact]
    public async Task Pay_StudentOwnPaymentViaAgent_RecordsAiAgentChannel()
    {
        var client = factory.CreateClientWithToken(factory.StudentToken("20230001"));
        client.DefaultRequestHeaders.Add("X-Client-Channel", "ai-agent");

        var response = await client.PostAsJsonAsync(Url, new { studentNo = "20230001", term = "2026-FALL", amount = 1000m, transactionId = "AGT-TEST-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("AI_AGENT", body.GetProperty("channel").GetString());
        Assert.Equal(2000.00m, body.GetProperty("remainingBalance").GetDecimal());
    }

    [Fact]
    public async Task Pay_NoToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, new { studentNo = "20230001", term = "2026-FALL", amount = 1m, transactionId = "T-NOAUTH" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Pay_AdminToken_Returns403()
    {
        var response = await PayAsync(factory.AdminToken(), new { studentNo = "20230001", term = "2026-FALL", amount = 1m, transactionId = "T-ADMIN" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Pay_TermWithoutRecord_Returns404()
    {
        var response = await PayAsync(factory.BankToken(), new { studentNo = "20230001", term = "2030-FALL", amount = 1m, transactionId = "T-NOREC" });

        await ProblemAssert.HasCode(response, HttpStatusCode.NotFound, ErrorCodes.TuitionNotFound);
    }
}
