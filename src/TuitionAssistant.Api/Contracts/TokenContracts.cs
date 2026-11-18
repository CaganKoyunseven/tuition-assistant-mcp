namespace TuitionAssistant.Api.Contracts;

/// <summary>
/// Token request (OAuth 2.0-style, JSON body).
/// Students and admins use <c>grantType = "password"</c> with <c>username</c> and <c>password</c>;
/// the banking app uses <c>grantType = "client_credentials"</c> with <c>clientId</c> and <c>clientSecret</c>.
/// </summary>
public class TokenRequest
{
    /// <summary><c>password</c> or <c>client_credentials</c>.</summary>
    /// <example>password</example>
    public string? GrantType { get; set; }

    /// <summary>Student number (students) or admin username. Required for the password grant.</summary>
    /// <example>20230001</example>
    public string? Username { get; set; }

    /// <summary>Required for the password grant.</summary>
    /// <example>Student#2026</example>
    public string? Password { get; set; }

    /// <summary>Banking app client id. Required for the client_credentials grant.</summary>
    public string? ClientId { get; set; }

    /// <summary>Banking app client secret. Required for the client_credentials grant.</summary>
    public string? ClientSecret { get; set; }
}

/// <summary>Issued access token.</summary>
public class TokenResponse
{
    /// <summary>RS256-signed JWT. Send it as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public required string AccessToken { get; init; }

    /// <example>Bearer</example>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>Lifetime in seconds.</summary>
    /// <example>3600</example>
    public int ExpiresIn { get; init; }

    /// <summary><c>student</c>, <c>bank</c> or <c>admin</c>.</summary>
    /// <example>student</example>
    public required string Role { get; init; }

    /// <summary>Set for students only.</summary>
    /// <example>20230001</example>
    public string? StudentNo { get; init; }
}
