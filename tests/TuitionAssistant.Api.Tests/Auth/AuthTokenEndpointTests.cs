using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Auth;

public class AuthTokenEndpointTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private const string TokenUrl = "/api/v1/auth/token";

    private Task<HttpResponseMessage> PostTokenAsync(object body) => factory.CreateClient().PostAsJsonAsync(TokenUrl, body);

    [Fact]
    public async Task Token_ValidStudentCredentials_ReturnsRs256JwtWithStudentClaims()
    {
        var response = await PostTokenAsync(new { grantType = "password", username = "20230001", password = "Student#2026" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(3600, token.ExpiresIn);
        Assert.Equal(Roles.Student, token.Role);
        Assert.Equal("20230001", token.StudentNo);

        var jwt = new JsonWebToken(token.AccessToken);
        Assert.Equal("RS256", jwt.Alg);
        Assert.Equal("tuition-auth", jwt.Issuer);
        Assert.Contains("tuition-api", jwt.Audiences);
        Assert.Equal("20230001", jwt.Subject);
        Assert.Equal(Roles.Student, jwt.GetClaim("role").Value);
        Assert.Equal("20230001", jwt.GetClaim("student_no").Value);
        Assert.False(string.IsNullOrEmpty(jwt.Id));
        Assert.Equal(TimeSpan.FromMinutes(60), jwt.ValidTo - jwt.IssuedAt);
    }

    [Fact]
    public async Task Token_ValidBankClientCredentials_ReturnsBankRoleWithoutStudentNo()
    {
        var response = await PostTokenAsync(new { grantType = "client_credentials", clientId = "bank-app", clientSecret = "BankSecret#2026" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.Equal(Roles.Bank, token!.Role);
        Assert.Null(token.StudentNo);
        Assert.False(new JsonWebToken(token.AccessToken).TryGetClaim("student_no", out _));
    }

    [Fact]
    public async Task Token_ValidAdminCredentials_ReturnsAdminRole()
    {
        var response = await PostTokenAsync(new { grantType = "password", username = "admin", password = "Admin#2026" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Roles.Admin, (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Role);
    }

    [Fact]
    public async Task Token_WrongPassword_Returns401InvalidCredentials()
    {
        var response = await PostTokenAsync(new { grantType = "password", username = "20230001", password = "wrong" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Token_UnknownUser_ReturnsSameErrorAsWrongPassword()
    {
        var unknown = await ProblemAssert.HasCode(
            await PostTokenAsync(new { grantType = "password", username = "99999999", password = "Student#2026" }),
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
        var wrong = await ProblemAssert.HasCode(
            await PostTokenAsync(new { grantType = "password", username = "20230001", password = "nope" }),
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);

        Assert.Equal(wrong.GetProperty("detail").GetString(), unknown.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Token_BankClientUsingPasswordGrant_Returns401()
    {
        var response = await PostTokenAsync(new { grantType = "password", username = "bank-app", password = "BankSecret#2026" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Token_StudentUsingClientCredentialsGrant_Returns401()
    {
        var response = await PostTokenAsync(new { grantType = "client_credentials", clientId = "20230001", clientSecret = "Student#2026" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Token_InactiveUser_Returns401()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TuitionDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Users.Add(new User { Id = Guid.NewGuid(), Username = "disabled-admin", Role = Roles.Admin, PasswordHash = hasher.Hash("Pass#1234"), IsActive = false });
            await db.SaveChangesAsync();
        }

        var response = await PostTokenAsync(new { grantType = "password", username = "disabled-admin", password = "Pass#1234" });

        await ProblemAssert.HasCode(response, HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
    }

    [Theory]
    [InlineData("implicit")]
    [InlineData("")]
    public async Task Token_UnknownGrantType_Returns400ValidationError(string grantType)
    {
        var response = await PostTokenAsync(new { grantType, username = "20230001", password = "Student#2026" });

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Token_MissingPassword_Returns400ValidationError()
    {
        var response = await PostTokenAsync(new { grantType = "password", username = "20230001" });

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Token_MalformedJson_Returns400ValidationError()
    {
        var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await factory.CreateClient().PostAsync(TokenUrl, content);

        await ProblemAssert.HasCode(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Token_ResponseReportsSupportedApiVersions()
    {
        var response = await PostTokenAsync(new { grantType = "password", username = "admin", password = "Admin#2026" });

        Assert.True(response.Headers.TryGetValues("api-supported-versions", out var versions));
        Assert.Contains("1.0", versions);
    }

    [Fact]
    public async Task Token_UnversionedRoute_Returns404()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/token",
            new { grantType = "password", username = "admin", password = "Admin#2026" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
