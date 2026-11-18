using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TuitionAssistant.Api.Domain;

namespace TuitionAssistant.Api.Security;

/// <summary>Issues RS256-signed access tokens.</summary>
public class JwtTokenIssuer(JwtKeys keys, IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public int LifetimeSeconds => options.Value.LifetimeMinutes * 60;

    /// <summary>
    /// Creates a token with claims <c>iss, aud, sub, role, student_no (students only), jti, iat, nbf, exp</c>.
    /// </summary>
    public string Issue(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;

        var claims = new Dictionary<string, object>
        {
            [TuitionClaims.Subject] = user.Username,
            [TuitionClaims.Role] = user.Role,
            [TuitionClaims.TokenId] = Guid.NewGuid().ToString("N"),
        };
        if (user.Role == Roles.Student && user.StudentNo is not null)
        {
            claims[TuitionClaims.StudentNo] = user.StudentNo;
        }

        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(settings.LifetimeMinutes),
            SigningCredentials = new SigningCredentials(keys.SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
