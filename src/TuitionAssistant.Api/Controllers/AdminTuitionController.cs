using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Controllers;

/// <summary>University administration: add tuition (single and batch) and the unpaid tuition report.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/tuition")]
[Authorize(Policy = Policies.RequireAdmin)]
public class AdminTuitionController(
    TuitionAdminService adminService,
    TuitionImportService importService,
    UnpaidReportService unpaidReportService) : ControllerBase
{
    private static readonly string[] AcceptedCsvContentTypes =
        ["text/csv", "application/vnd.ms-excel", "text/plain", "application/octet-stream"];

    /// <summary>Adds or updates the assessed tuition for a student and term.</summary>
    /// <remarks>
    /// Idempotent upsert keyed by (studentNo, term): a new record returns 201 <c>CREATED</c>; the same amount again
    /// returns 200 <c>UNCHANGED</c>; a different amount returns 200 <c>UPDATED</c>, unless it is below the amount
    /// already paid (422 <c>AMOUNT_BELOW_PAID</c>). Unknown student numbers are created automatically
    /// (<c>studentCreated: true</c>). Only USD is supported.
    /// </remarks>
    /// <param name="request">Student number, term, amount and currency.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="201">Record created.</response>
    /// <response code="200">Record updated or unchanged.</response>
    /// <response code="400">Invalid field (<c>INVALID_TERM</c>, <c>UNSUPPORTED_CURRENCY</c>, …).</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="422">New amount is below the amount already paid.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<AddTuitionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<AddTuitionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddTuitionResponse>> Add([FromBody] AddTuitionRequest request, CancellationToken cancellationToken)
    {
        var result = await adminService.AddOrUpdateAsync(request, cancellationToken);
        return result.Status == TuitionAdminService.Created
            ? StatusCode(StatusCodes.Status201Created, result)
            : Ok(result);
    }

    /// <summary>Adds or updates tuition for many students from a CSV file.</summary>
    /// <remarks>
    /// Upload a CSV (field name <c>file</c>, max 1 MB, max 5,000 rows) with the header
    /// <c>studentNo,term,amount,currency</c>. Each row is processed independently with the same rules as
    /// Add Tuition; the response reports success/failure counts and per-line errors.
    /// </remarks>
    /// <param name="file">CSV file.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Batch processed (some rows may have failed; see <c>errors</c>).</response>
    /// <response code="400">Missing file, wrong header or no data rows (<c>INVALID_CSV</c>).</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="413">File larger than 1 MB (<c>PAYLOAD_TOO_LARGE</c>).</response>
    [HttpPost("batch")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    [ProducesResponseType<BatchImportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<BatchImportResponse>> AddBatch(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw ApiException.Validation("Upload a CSV file in the multipart field 'file'.", ErrorCodes.InvalidCsv);
        }

        if (file.Length > TuitionImportService.MaxFileBytes)
        {
            throw new ApiException(StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge, "Payload too large",
                $"The CSV file must not be larger than {TuitionImportService.MaxFileBytes / 1024} KB.");
        }

        var contentType = file.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!AcceptedCsvContentTypes.Contains(contentType) && !file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Validation($"Content type '{file.ContentType}' is not a CSV file.", ErrorCodes.InvalidCsv);
        }

        await using var stream = file.OpenReadStream();
        return Ok(await importService.ImportAsync(stream, file.FileName, StudentAccess.Subject(User), cancellationToken));
    }

    /// <summary>Lists students with unpaid tuition for a term (paged).</summary>
    /// <remarks>
    /// Returns records with a balance greater than zero, ordered by balance (highest first), then student number.
    /// A page beyond the last page returns an empty <c>items</c> list.
    /// </remarks>
    /// <param name="term">Required term, e.g. <c>2026-FALL</c>.</param>
    /// <param name="page">Page number (≥ 1, default 1).</param>
    /// <param name="pageSize">Page size (1–100, default 20).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">One page of unpaid records.</response>
    /// <response code="400">Missing/invalid term or invalid paging values.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet("unpaid")]
    [ProducesResponseType<UnpaidTuitionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UnpaidTuitionResponse>> Unpaid(
        [FromQuery] string? term, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        Ok(await unpaidReportService.GetAsync(term, page, pageSize, cancellationToken));
}
