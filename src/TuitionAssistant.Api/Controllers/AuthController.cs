using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuitionAssistant.Api.Application;
using TuitionAssistant.Api.Contracts;

namespace TuitionAssistant.Api.Controllers;

/// <summary>Authentication: issues JWT access tokens.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    /// <summary>Issues an access token (OAuth 2.0 password and client-credentials grants, JSON body).</summary>
    /// <remarks>
    /// Students log in with their student number and password, admins with their username and password
    /// (<c>grantType = "password"</c>). The banking app is a machine client and uses
    /// <c>grantType = "client_credentials"</c>. The token is valid for 60 minutes.
    /// At the gateway this endpoint is rate-limited to 10 requests per minute per IP.
    /// </remarks>
    /// <response code="200">Token issued.</response>
    /// <response code="400">Missing fields or unknown grant type (<c>VALIDATION_ERROR</c>).</response>
    /// <response code="401">Invalid credentials (<c>INVALID_CREDENTIALS</c>).</response>
    [HttpPost("token")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Token([FromBody] TokenRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.IssueTokenAsync(request, cancellationToken));
}
