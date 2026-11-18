using System.Net;
using System.Net.Http.Json;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

public class AddTuitionEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private const string Url = "/api/v1/admin/tuition";

    private Task<HttpResponseMessage> AddAsync(object body, string? token = null) =>
        factory.CreateClientWithToken(token ?? factory.AdminToken()).PostAsJsonAsync(Url, body);

    [Fact]
    public async Task Add_NewRecordForKnownStudent_Returns201Created()
    {
        var response = await AddAsync(new { studentNo = "20230001", term = "2027-SPRING", amount = 6000.00m, currency = "USD" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("CREATED", body.GetProperty("status").GetString());
        Assert.Equal(6000.00m, body.GetProperty("balance").GetDecimal());
        Assert.False(body.GetProperty("studentCreated").GetBoolean());
    }

    [Fact]
    public async Task Add_UnknownStudent_IsAutoCreated()
    {
        var response = await AddAsync(new { studentNo = "20230005", term = "2027-SPRING", amount = 6000m, currency = "usd" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.True(body.GetProperty("studentCreated").GetBoolean());
        Assert.Equal("USD", body.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Add_SameAmountAgain_Returns200Unchanged()
    {
        var response = await AddAsync(new { studentNo = "20230004", term = "2026-FALL", amount = 4500m, currency = "USD" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("UNCHANGED", (await response.ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Add_HigherAmount_Returns200UpdatedAndRecomputesBalance()
    {
        // 20230001 / 2026-FALL: total 6,000, paid 3,000.
        var response = await AddAsync(new { studentNo = "20230001", term = "2026-FALL", amount = 6500m, currency = "USD" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("UPDATED", body.GetProperty("status").GetString());
        Assert.Equal(6500.00m, body.GetProperty("tuitionTotal").GetDecimal());
        Assert.Equal(3000.00m, body.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(3500.00m, body.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Add_AmountBelowPaid_Returns422AndKeepsRecord()
    {
        // 20230003 / 2026-FALL: paid 5,750.
        var response = await AddAsync(new { studentNo = "20230003", term = "2026-FALL", amount = 5000m, currency = "USD" });

        var body = await ProblemAssert.HasCode(response, HttpStatusCode.UnprocessableEntity, ErrorCodes.AmountBelowPaid);
        Assert.Equal(5750.00m, body.GetProperty("paidAmount").GetDecimal());
    }

    [Fact]
    public async Task Add_EurCurrency_Returns400UnsupportedCurrency()
    {
        var response = await AddAsync(new { studentNo = "20230004", term = "2027-SPRING", amount = 100m, currency = "EUR" });

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.UnsupportedCurrency);
    }

    [Fact]
    public async Task Add_BadTerm_Returns400InvalidTerm()
    {
        var response = await AddAsync(new { studentNo = "20230004", term = "FALL-2026", amount = 100m, currency = "USD" });

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.InvalidTerm);
    }

    [Fact]
    public async Task Add_StudentToken_Returns403()
    {
        var response = await AddAsync(new { studentNo = "20230001", term = "2027-SUMMER", amount = 1m, currency = "USD" }, factory.StudentToken());

        await ProblemAssert.HasCode(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Add_NoToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, new { studentNo = "20230001", term = "2027-SUMMER", amount = 1m, currency = "USD" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }
}
