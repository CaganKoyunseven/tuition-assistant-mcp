using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Controllers;

/// <summary>Pay Tuition (authenticated, simulated payments).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/banking/payments")]
[Authorize(Policy = Policies.RequireStudentOrBank)]
public class BankingPaymentsController(PaymentService paymentService) : ControllerBase
{
    /// <summary>Records a partial or full tuition payment.</summary>
    /// <remarks>
    /// Payments are simulated (no card processing) and post immediately. Rules:
    /// duplicate <c>transactionId</c> → 409 <c>DUPLICATE_TRANSACTION</c> (with the original payment);
    /// amount above the outstanding balance → 422 <c>AMOUNT_EXCEEDS_BALANCE</c>; fully paid term → 422 <c>ALREADY_PAID</c>.
    /// Role <c>bank</c> may pay for any student; role <c>student</c> only for itself.
    /// The AI agent sends <c>X-Client-Channel: ai-agent</c> so the payment is recorded with channel <c>AI_AGENT</c>.
    /// </remarks>
    /// <param name="request">Student number, term, amount and a unique transaction ID.</param>
    /// <param name="clientChannel">Optional <c>X-Client-Channel</c> header (<c>ai-agent</c>).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="201">Payment recorded.</response>
    /// <response code="400">Invalid field (<c>INVALID_AMOUNT</c>, <c>INVALID_TERM</c>, …).</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Role not allowed, or a student paying for another student.</response>
    /// <response code="404">No tuition record for the student and term.</response>
    /// <response code="409">Duplicate transaction ID or concurrent update.</response>
    /// <response code="422">Amount exceeds the balance, or the term is already paid.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaymentResponse>> Pay(
        [FromBody] PayTuitionRequest request,
        [FromHeader(Name = PaymentService.ClientChannelHeader)] string? clientChannel,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.PayAsync(request, User, clientChannel, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
