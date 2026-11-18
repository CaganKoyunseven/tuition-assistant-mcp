using System.Net;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Endpoints;

public class CrossCuttingTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    [Fact]
    public async Task Swagger_DocumentListsAllOperationsAndBearerScheme()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.ReadJsonAsync();
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/v1/auth/token", paths);
        Assert.Contains("/api/v1/mobile/tuition/{studentNo}", paths);
        Assert.Contains("/api/v1/banking/tuition/{studentNo}", paths);
        Assert.Contains("/api/v1/banking/payments", paths);
        Assert.Contains("/api/v1/admin/tuition", paths);
        Assert.Contains("/api/v1/admin/tuition/batch", paths);
        Assert.Contains("/api/v1/admin/tuition/unpaid", paths);
        Assert.True(doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));

        // Protected operations declare the bearer requirement; the mobile query does not.
        var payments = doc.GetProperty("paths").GetProperty("/api/v1/banking/payments").GetProperty("post");
        Assert.True(payments.TryGetProperty("security", out _));
        var mobile = doc.GetProperty("paths").GetProperty("/api/v1/mobile/tuition/{studentNo}").GetProperty("get");
        Assert.False(mobile.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task SwaggerUi_IsServed()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnversionedRoute_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/api/mobile/tuition/20230001");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VersionedResponse_ReportsSupportedVersions()
    {
        var response = await factory.CreateClientWithToken(factory.BankToken()).GetAsync("/api/v1/banking/tuition/20230001");

        Assert.Contains("1.0", response.Headers.GetValues("api-supported-versions"));
    }
}
