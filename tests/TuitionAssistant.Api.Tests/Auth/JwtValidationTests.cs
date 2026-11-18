using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TuitionAssistant.Api.Tests.Infrastructure;

namespace TuitionAssistant.Api.Tests.Auth;

/// <summary>Uses the exact validation parameters configured for the API's JwtBearer handler.</summary>
public class JwtValidationTests(TuitionApiFactory factory) : IClassFixture<TuitionApiFactory>
{
    private readonly JsonWebTokenHandler _handler = new();

    private TokenValidationParameters Parameters => factory.Services
        .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
        .Get(JwtBearerDefaults.AuthenticationScheme)
        .TokenValidationParameters;

    private string Sign(SecurityKey key, string audience = "tuition-api", DateTime? expires = null, string issuer = "tuition-auth")
    {
        var now = DateTime.UtcNow;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = "20230001", ["role"] = "student", ["student_no"] = "20230001" },
            IssuedAt = now.AddHours(-2),
            NotBefore = now.AddHours(-2),
            Expires = expires ?? now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    [Fact]
    public async Task IssuedToken_IsValid()
    {
        var result = await _handler.ValidateTokenAsync(factory.StudentToken(), Parameters);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("20230001", result.ClaimsIdentity.Name);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        var result = await _handler.ValidateTokenAsync(Sign(new RsaSecurityKey(TestKeys.Other)), Parameters);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TamperedPayload_IsRejected()
    {
        var parts = factory.StudentToken("20230001").Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1]).Replace("20230001", "20230002");
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        var result = await _handler.ValidateTokenAsync(forged, Parameters);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var signingKey = new RsaSecurityKey(System.Security.Cryptography.RSA.Create());
        signingKey.Rsa.ImportFromPem(TestKeys.PrivateKeyPem);

        var result = await _handler.ValidateTokenAsync(Sign(signingKey, expires: DateTime.UtcNow.AddMinutes(-5)), Parameters);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task WrongAudienceOrIssuer_IsRejected()
    {
        var signingKey = new RsaSecurityKey(System.Security.Cryptography.RSA.Create());
        signingKey.Rsa.ImportFromPem(TestKeys.PrivateKeyPem);

        Assert.False((await _handler.ValidateTokenAsync(Sign(signingKey, audience: "other-api"), Parameters)).IsValid);
        Assert.False((await _handler.ValidateTokenAsync(Sign(signingKey, issuer: "evil"), Parameters)).IsValid);
    }
}
