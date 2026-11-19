using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TuitionAssistant.Agent.Configuration;
using TuitionAssistant.Agent.Tools;

namespace TuitionAssistant.Agent.Security;

/// <summary>Validates the student's JWT with the public key (the agent never holds the signing key).</summary>
public static class AgentAuthentication
{
    public const string StudentPolicy = "RequireStudent";

    public static IServiceCollection AddAgentAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;
                if (string.IsNullOrWhiteSpace(settings.PublicKeyPem))
                {
                    throw new InvalidOperationException("Jwt:PublicKeyPem is not configured (set JWT_PUBLIC_KEY_PEM in .env).");
                }

                var rsa = RSA.Create();
                rsa.ImportFromPem(settings.PublicKeyPem.Replace("\\n", "\n").Trim());
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
            });

        services.AddAuthorization(options => options.AddPolicy(StudentPolicy, p => p.RequireAuthenticatedUser().RequireRole("student")));
        return services;
    }

    /// <summary>Builds the tool context from the validated token: the student number is never taken from the request body.</summary>
    public static ToolContext ToToolContext(this HttpContext http, string sessionId)
    {
        var studentNo = http.User.FindFirstValue("student_no");
        var header = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(studentNo) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentException(StatusCodes.Status403Forbidden, AgentErrorCodes.SessionForbidden, "The token has no student number.");
        }

        return new ToolContext(studentNo, header["Bearer ".Length..].Trim(), sessionId);
    }
}
