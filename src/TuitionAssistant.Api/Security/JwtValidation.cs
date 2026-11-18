using Microsoft.IdentityModel.Tokens;

namespace TuitionAssistant.Api.Security;

/// <summary>
/// Token validation rules shared by the API's JwtBearer handler (defense in depth: the API never
/// trusts the gateway blindly; Kong validates the same signature and expiry first).
/// </summary>
public static class JwtValidation
{
    public static TokenValidationParameters CreateParameters(JwtOptions options, SecurityKey validationKey) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = validationKey,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        NameClaimType = TuitionClaims.Subject,
        RoleClaimType = TuitionClaims.Role,
    };
}
