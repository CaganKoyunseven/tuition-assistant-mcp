using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TuitionAssistant.Api.Contracts;

namespace TuitionAssistant.Api.Security;

/// <summary>Registers JWT bearer authentication, the authorization policies and token issuing.</summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddTuitionAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.PrivateKeyPem), "Jwt:PrivateKeyPem is required (run scripts/gen-keys.sh).")
            .Validate(o => o.LifetimeMinutes is > 0 and <= 1440, "Jwt:LifetimeMinutes must be between 1 and 1440.")
            .ValidateOnStart();

        services.AddSingleton<JwtKeys>();
        services.AddSingleton<JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from options at runtime so tests and environments can supply their own keys.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeys, IOptions<JwtOptions>>((bearer, keys, jwt) =>
            {
                bearer.MapInboundClaims = false; // keep "sub", "role", "student_no" exactly as issued
                bearer.TokenValidationParameters = JwtValidation.CreateParameters(jwt.Value, keys.ValidationKey);
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        return WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
                            "Unauthorized", "A valid bearer token is required.", addChallengeHeader: true);
                    },
                    OnForbidden = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden,
                        "Forbidden", "Your role is not allowed to perform this operation."),
                };
            });

        services.AddAuthorization(options => options.AddTuitionPolicies());
        return services;
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string code, string title, string detail, bool addChallengeHeader = false)
    {
        httpContext.Response.StatusCode = status;
        if (addChallengeHeader)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;

        var writer = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await writer.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
