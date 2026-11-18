namespace TuitionAssistant.Api.Security;

/// <summary>JWT settings (configuration section <c>Jwt</c>; keys come from environment variables).</summary>
public class JwtOptions
{
    /// <summary>Token issuer. Kong uses this claim to find the verification key, so it must match kong.yml.</summary>
    public string Issuer { get; set; } = "tuition-auth";

    public string Audience { get; set; } = "tuition-api";

    public int LifetimeMinutes { get; set; } = 60;

    /// <summary>RSA private key (PEM) used to sign tokens. Only the Tuition API has it. Env: <c>Jwt__PrivateKeyPem</c>.</summary>
    public string? PrivateKeyPem { get; set; }

    /// <summary>RSA public key (PEM). Optional here: derived from the private key when absent.</summary>
    public string? PublicKeyPem { get; set; }
}
