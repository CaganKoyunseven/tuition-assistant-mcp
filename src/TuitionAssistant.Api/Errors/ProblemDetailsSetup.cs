using System.Diagnostics;
using TuitionAssistant.Api.Contracts;

namespace TuitionAssistant.Api.Errors;

/// <summary>Makes every error response an RFC 7807 ProblemDetails body with <c>code</c> and <c>traceId</c>.</summary>
public static class ProblemDetailsSetup
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;

            problem.Type = $"https://httpstatuses.io/{status}";
            problem.Instance ??= context.HttpContext.Request.Path;
            problem.Extensions.TryAdd("code", ErrorCodes.ForStatus(status));
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
        return services;
    }
}
