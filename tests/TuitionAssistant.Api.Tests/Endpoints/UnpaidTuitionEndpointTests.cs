using System.Net;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

/// <summary>
/// Seed data for 2026-FALL has 28 records with a balance &gt; 0: 20230001, 20230003, 20230004 and the
/// 25 paging students. 20230002 is fully paid. This class never modifies data.
/// </summary>
public class UnpaidTuitionEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private Task<HttpResponseMessage> GetAsync(string query, string? token = null) =>
        factory.CreateClientWithToken(token ?? factory.AdminToken()).GetAsync($"/api/v1/admin/tuition/unpaid{query}");

    [Fact]
    public async Task Unpaid_FirstPage_Returns20ItemsOrderedByBalanceDesc()
    {
        var response = await GetAsync("?term=2026-FALL&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(28, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());

        var balances = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("balance").GetDecimal()).ToList();
        Assert.Equal(20, balances.Count);
        Assert.Equal(balances.OrderByDescending(b => b), balances);
        Assert.Equal(4500.00m, balances[0]); // 20230004 has the highest balance

        // 3000 + 250 + 4500 + Σ(1000 + 137·i, i = 1..25) = 7750 + 25000 + 44525
        Assert.Equal(77275.00m, body.GetProperty("totalOutstanding").GetDecimal());
    }

    [Fact]
    public async Task Unpaid_SecondPage_ReturnsRemainingItems()
    {
        var body = await (await GetAsync("?term=2026-FALL&page=2&pageSize=20")).ReadJsonAsync();

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(8, items.Count);
        Assert.Equal(250.00m, items[^1].GetProperty("balance").GetDecimal()); // 20230003 has the lowest balance
    }

    [Fact]
    public async Task Unpaid_FullyPaidStudents_AreExcluded()
    {
        var body = await (await GetAsync("?term=2026-FALL&pageSize=100")).ReadJsonAsync();

        var students = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("studentNo").GetString()).ToList();
        Assert.DoesNotContain("20230002", students);
        Assert.Equal(28, students.Count);
    }

    [Theory]
    [InlineData("?term=2026-FALL&pageSize=0")]
    [InlineData("?term=2026-FALL&pageSize=101")]
    [InlineData("?term=2026-FALL&page=0")]
    [InlineData("?term=2026-FALL&page=abc")]
    public async Task Unpaid_InvalidPaging_Returns400(string query)
    {
        await ProblemAssert.HasCode(await GetAsync(query), HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Unpaid_MissingTerm_Returns400()
    {
        await ProblemAssert.HasCode(await GetAsync(""), HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Unpaid_PageBeyondRange_Returns200Empty()
    {
        var response = await GetAsync("?term=2026-FALL&page=99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Unpaid_NonAdmin_Returns403()
    {
        await ProblemAssert.HasCode(await GetAsync("?term=2026-FALL", factory.StudentToken()), HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }
}
