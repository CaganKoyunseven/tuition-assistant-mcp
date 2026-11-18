using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Controllers;

/// <summary>Query Tuition for the banking app (authenticated).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/banking/tuition")]
[Authorize(Policy = Policies.RequireStudentOrBank)]
public class BankingTuitionController(TuitionQueryService queryService) : ControllerBase
{
    /// <summary>Returns the tuition total and balance for a student number (banking app).</summary>
    /// <remarks>
    /// Requires a bearer token. Role <c>bank</c> may query any student; role <c>student</c> may query only
    /// the student number in its own token (otherwise 403 <c>FORBIDDEN_NOT_OWNER</c>). The AI agent uses this
    /// endpoint with the signed-in student's token. Omit <c>term</c> for the overall balance.
    /// </remarks>
    /// <param name="studentNo">8-digit student number.</param>
    /// <param name="term">Optional term (<c>YYYY-FALL|SPRING|SUMMER</c>).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Tuition totals.</response>
    /// <response code="400">Invalid student number or term.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Role not allowed, or a student querying another student.</response>
    /// <response code="404">Unknown student, or no record for the term.</response>
    [HttpGet("{studentNo}")]
    [ProducesResponseType<TuitionQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TuitionQueryResponse>> Get(string studentNo, [FromQuery] string? term, CancellationToken cancellationToken)
    {
        var validStudentNo = Validation.StudentNo(studentNo);
        var validTerm = Validation.OptionalTerm(term);
        StudentAccess.EnsureCanAccessStudent(User, validStudentNo);

        return Ok(await queryService.GetAsync(validStudentNo, validTerm, cancellationToken));
    }
}
