using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;

namespace TuitionAssistant.Api.Controllers;

/// <summary>Query Tuition for the university mobile app (no authentication, rate-limited).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mobile/tuition")]
[AllowAnonymous]
public class MobileTuitionController(TuitionQueryService queryService, MobileQueryLimiter limiter) : ControllerBase
{
    /// <summary>Returns the tuition total and balance for a student number (mobile app).</summary>
    /// <remarks>
    /// No authentication, as required by the specification. Limited to **3 calls per student number per UTC day**
    /// (enforced at the gateway, keyed by the student number in the path, and again by a database counter here).
    /// Omit <c>term</c> for the overall balance across all terms; pass <c>term</c> (e.g. <c>2026-FALL</c>) for one term.
    /// Response headers <c>X-Daily-Limit</c> and <c>X-Daily-Remaining</c> show the backend counter.
    /// </remarks>
    /// <param name="studentNo">8-digit student number.</param>
    /// <param name="term">Optional term (<c>YYYY-FALL|SPRING|SUMMER</c>).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Tuition totals.</response>
    /// <response code="400">Invalid student number or term.</response>
    /// <response code="404">Unknown student, or no record for the term.</response>
    /// <response code="429">Daily limit reached (<c>RATE_LIMIT_EXCEEDED</c>, see <c>Retry-After</c>).</response>
    [HttpGet("{studentNo}")]
    [ProducesResponseType<TuitionQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TuitionQueryResponse>> Get(string studentNo, [FromQuery] string? term, CancellationToken cancellationToken)
    {
        var validStudentNo = Validation.StudentNo(studentNo);
        var validTerm = Validation.OptionalTerm(term);

        var remaining = await limiter.RegisterQueryAsync(validStudentNo, cancellationToken);
        Response.Headers["X-Daily-Limit"] = MobileQueryLimiter.DailyLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Daily-Remaining"] = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return Ok(await queryService.GetAsync(validStudentNo, validTerm, cancellationToken));
    }
}
