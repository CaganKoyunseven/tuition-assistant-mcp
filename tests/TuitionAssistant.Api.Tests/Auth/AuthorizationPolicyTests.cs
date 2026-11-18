using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TuitionAssistant.Api.Security;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Auth;

/// <summary>Real tokens → real validation → real policy evaluation.</summary>
public class AuthorizationPolicyTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private async Task<ClaimsPrincipal> PrincipalFromAsync(string token)
    {
        var parameters = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid);
        return new ClaimsPrincipal(result.ClaimsIdentity);
    }

    private async Task<bool> IsAllowedAsync(string token, string policy)
    {
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(await PrincipalFromAsync(token), policy)).Succeeded;
    }

    [Fact]
    public async Task StudentToken_AllowedForStudentPolicies_DeniedForAdmin()
    {
        var token = factory.StudentToken();

        Assert.True(await IsAllowedAsync(token, Policies.RequireStudentOrBank));
        Assert.True(await IsAllowedAsync(token, Policies.RequireStudent));
        Assert.False(await IsAllowedAsync(token, Policies.RequireAdmin));
    }

    [Fact]
    public async Task BankToken_AllowedForBanking_DeniedForStudentAndAdmin()
    {
        var token = factory.BankToken();

        Assert.True(await IsAllowedAsync(token, Policies.RequireStudentOrBank));
        Assert.False(await IsAllowedAsync(token, Policies.RequireStudent));
        Assert.False(await IsAllowedAsync(token, Policies.RequireAdmin));
    }

    [Fact]
    public async Task AdminToken_AllowedForAdminOnly()
    {
        var token = factory.AdminToken();

        Assert.True(await IsAllowedAsync(token, Policies.RequireAdmin));
        Assert.False(await IsAllowedAsync(token, Policies.RequireStudentOrBank));
        Assert.False(await IsAllowedAsync(token, Policies.RequireStudent));
    }

    [Fact]
    public async Task AnonymousUser_DeniedForEveryPolicy()
    {
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        foreach (var policy in new[] { Policies.RequireStudentOrBank, Policies.RequireStudent, Policies.RequireAdmin })
        {
            Assert.False((await authorization.AuthorizeAsync(anonymous, policy)).Succeeded);
        }
    }
}
