using System.Net;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

public class MobileTuitionEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private Task<HttpResponseMessage> GetAsync(string path) => factory.CreateClient().GetAsync($"/api/v1/mobile/tuition/{path}");

    [Fact]
    public async Task Query_KnownStudentWithoutTerm_ReturnsOverallTotalsAndTermBreakdown()
    {
        var response = await GetAsync("20230002");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("OVERALL", body.GetProperty("scope").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("term").ValueKind);
        Assert.Equal(9000.00m, body.GetProperty("tuitionTotal").GetDecimal());
        Assert.Equal(4500.00m, body.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(4500.00m, body.GetProperty("balance").GetDecimal());
        var terms = body.GetProperty("terms").EnumerateArray().Select(t => t.GetProperty("term").GetString()).ToList();
        Assert.Equal(["2026-FALL", "2027-SPRING"], terms);
        Assert.Equal("3", response.Headers.GetValues("X-Daily-Limit").Single());
        Assert.Equal("2", response.Headers.GetValues("X-Daily-Remaining").Single());
    }

    [Fact]
    public async Task Query_KnownStudentWithTerm_ReturnsTermScope()
    {
        var response = await GetAsync("20230001?term=2026-fall");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("TERM", body.GetProperty("scope").GetString());
        Assert.Equal("2026-FALL", body.GetProperty("term").GetString());
        Assert.Equal(6000.00m, body.GetProperty("tuitionTotal").GetDecimal());
        Assert.Equal(3000.00m, body.GetProperty("balance").GetDecimal());
        Assert.Equal("PARTIALLY_PAID", body.GetProperty("status").GetString());
        Assert.Contains("\"balance\":3000.00", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("2023001")]
    [InlineData("2023000A")]
    [InlineData("202300011")]
    public async Task Query_InvalidStudentNoFormat_Returns400(string studentNo)
    {
        await ProblemAssert.HasCode(await GetAsync(studentNo), HttpStatusCode.BadRequest, ErrorCodes.InvalidStudentNo);
    }

    [Fact]
    public async Task Query_InvalidTerm_Returns400()
    {
        await ProblemAssert.HasCode(await GetAsync("20230003?term=2026-WINTER"), HttpStatusCode.BadRequest, ErrorCodes.InvalidTerm);
    }

    [Fact]
    public async Task Query_UnknownStudent_Returns404()
    {
        await ProblemAssert.HasCode(await GetAsync("29999999"), HttpStatusCode.NotFound, ErrorCodes.StudentNotFound);
    }

    [Fact]
    public async Task Query_TermWithoutRecord_Returns404TuitionNotFound()
    {
        await ProblemAssert.HasCode(await GetAsync("20239010?term=2030-FALL"), HttpStatusCode.NotFound, ErrorCodes.TuitionNotFound);
    }

    [Fact]
    public async Task Query_FourthCallSameStudentSameDay_Returns429_OtherStudentStillAllowed()
    {
        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync("20230004")).StatusCode);
        }

        var fourth = await GetAsync("20230004");
        await ProblemAssert.HasCode(fourth, HttpStatusCode.TooManyRequests, ErrorCodes.RateLimitExceeded);
        var retryAfter = int.Parse(fourth.Headers.GetValues("Retry-After").Single());
        Assert.InRange(retryAfter, 1, 86_400);
        Assert.Equal("0", fourth.Headers.GetValues("X-Daily-Remaining").Single());

        // Keyed by student number, not by caller: another student is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("20230003")).StatusCode);
    }

    [Fact]
    public async Task Query_DoesNotRequireAuthentication()
    {
        var client = factory.CreateClient();
        Assert.Null(client.DefaultRequestHeaders.Authorization);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/mobile/tuition/20239011")).StatusCode);
    }
}
