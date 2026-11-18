using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Contracts;

namespace TuitionAssistant.Api.Errors;

/// <summary>
/// Converts exceptions into ProblemDetails responses. Known <see cref="ApiException"/>s keep their
/// status and code; anything else becomes a generic 500 without stack traces in the body.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case ApiException apiException:
                problem = apiException.ToProblemDetails();
                foreach (var (name, value) in apiException.Headers)
                {
                    httpContext.Response.Headers[name] = value;
                }

                break;
            case BadHttpRequestException badRequest:
                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Payload too large" : "Bad request",
                    Detail = badRequest.Message,
                };
                problem.Extensions["code"] = ErrorCodes.ForStatus(badRequest.StatusCode);
                break;
            default:
                logger.LogError(exception, "Unhandled exception");
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal server error",
                    Detail = "An unexpected error occurred.",
                };
                problem.Extensions["code"] = ErrorCodes.InternalError;
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
