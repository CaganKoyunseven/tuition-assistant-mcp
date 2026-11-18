using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Contracts;

namespace TuitionAssistant.Api.Errors;

/// <summary>
/// A business or validation error that maps to an RFC 7807 ProblemDetails response.
/// Thrown by services; converted to an HTTP response by <see cref="ApiExceptionHandler"/>.
/// </summary>
public class ApiException(int status, string code, string title, string detail) : Exception(detail)
{
    public int Status { get; } = status;

    public string Code { get; } = code;

    public string Title { get; } = title;

    /// <summary>Additional fields added to the ProblemDetails body (e.g. <c>currentBalance</c>).</summary>
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>();

    public ApiException With(string key, object? value)
    {
        Extensions[key] = value;
        return this;
    }

    public ProblemDetails ToProblemDetails()
    {
        var problem = new ProblemDetails { Status = Status, Title = Title, Detail = Message };
        problem.Extensions["code"] = Code;
        foreach (var (key, value) in Extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

    public static ApiException Validation(string detail, string code = ErrorCodes.ValidationError) =>
        new(StatusCodes.Status400BadRequest, code, "Validation failed", detail);

    public static ApiException Forbidden(string detail, string code = ErrorCodes.Forbidden) =>
        new(StatusCodes.Status403Forbidden, code, "Forbidden", detail);
}
