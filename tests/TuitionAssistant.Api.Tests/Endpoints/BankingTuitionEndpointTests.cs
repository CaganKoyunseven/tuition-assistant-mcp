using System.Net;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

public class BankingTuitionEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private const string Url = "/api/v1/banking/tuition/";

    [Fact]
    public async Task Query_BankToken_AnyStudent_Returns200()
    {
        var response = await factory.CreateClientWithToken(factory.BankToken()).GetAsync(Url + "20230002?term=2027-SPRING");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(4500.00m, body.GetProperty("balance").GetDecimal());
        Assert.Equal("UNPAID", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Query_StudentToken_OwnNumber_Returns200()
    {
        var response = await factory.CreateClientWithToken(factory.StudentToken("20230001")).GetAsync(Url + "20230001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3000.00m, (await response.ReadJsonAsync()).GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Query_StudentToken_OtherStudent_Returns403NotOwner()
    {
        var response = await factory.CreateClientWithToken(factory.StudentToken("20230001")).GetAsync(Url + "20230002");

        await ProblemAssert.HasCode(response, HttpStatusCode.Forbidden, ErrorCodes.ForbiddenNotOwner);
    }

    [Fact]
    public async Task Query_NoToken_Returns401WithChallenge()
    {
        var response = await factory.CreateClient().GetAsync(Url + "20230001");

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task Query_GarbageToken_Returns401()
    {
        var response = await factory.CreateClientWithToken("not-a-jwt").GetAsync(Url + "20230001");

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Query_AdminToken_Returns403()
    {
        var response = await factory.CreateClientWithToken(factory.AdminToken()).GetAsync(Url + "20230001");

        await ProblemAssert.HasCode(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Query_UnknownTerm_Returns404TuitionNotFound()
    {
        var response = await factory.CreateClientWithToken(factory.BankToken()).GetAsync(Url + "20230001?term=2031-SPRING");

        await ProblemAssert.HasCode(response, HttpStatusCode.NotFound, ErrorCodes.TuitionNotFound);
    }

    [Fact]
    public async Task Query_InvalidStudentNo_Returns400()
    {
        var response = await factory.CreateClientWithToken(factory.BankToken()).GetAsync(Url + "abc");

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.InvalidStudentNo);
    }
}
