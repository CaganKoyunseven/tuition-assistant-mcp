using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

public class BatchImportEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private const string Url = "/api/v1/admin/tuition/batch";

    private Task<HttpResponseMessage> UploadAsync(string csv, string? token = null, string contentType = "text/csv")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", "tuition.csv");
        return factory.CreateClientWithToken(token ?? factory.AdminToken()).PostAsync(Url, content);
    }

    [Fact]
    public async Task Batch_AllValidRows_ReportsCreatedAndUpdatedCounts()
    {
        const string csv = """
            studentNo,term,amount,currency
            20240101,2027-SPRING,5000.00,USD
            20240102,2027-SPRING,5500.00,USD
            20230004,2026-FALL,4800.00,USD
            """;

        var response = await UploadAsync(csv);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(3, body.GetProperty("totalRows").GetInt32());
        Assert.Equal(3, body.GetProperty("succeeded").GetInt32());
        Assert.Equal(0, body.GetProperty("failed").GetInt32());
        Assert.Equal(2, body.GetProperty("created").GetInt32());
        Assert.Equal(1, body.GetProperty("updated").GetInt32());
        Assert.Equal(2, body.GetProperty("studentsCreated").GetInt32());
        Assert.Empty(body.GetProperty("errors").EnumerateArray());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TuitionDbContext>();
        Assert.True(await db.BatchImports.AnyAsync(b => b.TotalRows == 3 && b.UploadedBy == "admin"));
    }

    [Fact]
    public async Task Batch_MixedRows_ReportsPartialSuccessWithPerLineErrors()
    {
        const string csv = """
            studentNo,term,amount,currency
            20240201,2027-SPRING,5000.00,USD
            2024X201,2027-SPRING,5000.00,USD
            20240202,2027-SPRING,5000.00,EUR
            20230003,2026-FALL,1000.00,USD
            20240203,2027-WINTER,5000.00,USD
            20240204,2027-SPRING,abc,USD
            """;

        var response = await UploadAsync(csv);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(6, body.GetProperty("totalRows").GetInt32());
        Assert.Equal(1, body.GetProperty("succeeded").GetInt32());
        Assert.Equal(5, body.GetProperty("failed").GetInt32());

        var errors = body.GetProperty("errors").EnumerateArray()
            .Select(e => (Line: e.GetProperty("line").GetInt32(), Code: e.GetProperty("code").GetString()))
            .ToList();
        Assert.Equal(
            [(3, ErrorCodes.InvalidStudentNo), (4, ErrorCodes.UnsupportedCurrency), (5, ErrorCodes.AmountBelowPaid), (6, ErrorCodes.InvalidTerm), (7, ErrorCodes.InvalidAmount)],
            errors);
    }

    [Fact]
    public async Task Batch_WrongHeader_Returns400InvalidCsv()
    {
        await ProblemAssert.HasCode(await UploadAsync("student,term,amount,currency\n20230001,2026-FALL,1.00,USD"),
            HttpStatusCode.BadRequest, ErrorCodes.InvalidCsv);
    }

    [Fact]
    public async Task Batch_HeaderOnly_Returns400InvalidCsv()
    {
        await ProblemAssert.HasCode(await UploadAsync("studentNo,term,amount,currency\n"), HttpStatusCode.BadRequest, ErrorCodes.InvalidCsv);
    }

    [Fact]
    public async Task Batch_MissingFile_Returns400InvalidCsv()
    {
        var response = await factory.CreateClientWithToken(factory.AdminToken()).PostAsync(Url, new MultipartFormDataContent { { new StringContent("x"), "other" } });

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.InvalidCsv);
    }

    [Fact]
    public async Task Batch_FileOver1MB_Returns413()
    {
        var csv = "studentNo,term,amount,currency\n" + new string('x', 1_048_577);

        await ProblemAssert.HasCode(await UploadAsync(csv), HttpStatusCode.RequestEntityTooLarge, ErrorCodes.PayloadTooLarge);
    }

    [Fact]
    public async Task Batch_NonAdmin_Returns403()
    {
        await ProblemAssert.HasCode(await UploadAsync("studentNo,term,amount,currency\n20230001,2026-FALL,1.00,USD", factory.BankToken()),
            HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }
}
