using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TuitionAssistant.Api.Security;

/// <summary>Loads the RS256 signing and validation keys once at startup.</summary>
public sealed class JwtKeys
{
    private const int MinimumKeySizeBits = 2048;

    public JwtKeys(IOptions<JwtOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.PrivateKeyPem))
        {
            throw new InvalidOperationException(
                "Jwt:PrivateKeyPem is not configured. Run scripts/gen-keys.sh and set JWT_PRIVATE_KEY_PEM in .env.");
        }

        var privateKey = RSA.Create();
        privateKey.ImportFromPem(NormalizePem(settings.PrivateKeyPem));
        if (privateKey.KeySize < MinimumKeySizeBits)
        {
            throw new InvalidOperationException($"The JWT RSA key must be at least {MinimumKeySizeBits} bits.");
        }

        var publicKey = RSA.Create();
        if (string.IsNullOrWhiteSpace(settings.PublicKeyPem))
        {
            publicKey.ImportParameters(privateKey.ExportParameters(includePrivateParameters: false));
        }
        else
        {
            publicKey.ImportFromPem(NormalizePem(settings.PublicKeyPem));
        }

        SigningKey = new RsaSecurityKey(privateKey);
        ValidationKey = new RsaSecurityKey(publicKey);
    }

    public RsaSecurityKey SigningKey { get; }

    public RsaSecurityKey ValidationKey { get; }

    /// <summary>Environment variables carry PEMs on one line with literal "\n" sequences; restore real newlines.</summary>
    public static string NormalizePem(string pem) => pem.Replace("\\n", "\n").Trim();
}
