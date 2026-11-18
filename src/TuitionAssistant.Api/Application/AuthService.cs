using Microsoft.EntityFrameworkCore;
using TuitionAssistant.Api.Contracts;
using TuitionAssistant.Api.Domain;
using TuitionAssistant.Api.Errors;
using TuitionAssistant.Api.Infrastructure;
using TuitionAssistant.Api.Security;

namespace TuitionAssistant.Api.Application;

/// <summary>Verifies credentials and issues access tokens.</summary>
public class AuthService(TuitionDbContext db, IPasswordHasher passwordHasher, JwtTokenIssuer tokenIssuer)
{
    public const string PasswordGrant = "password";
    public const string ClientCredentialsGrant = "client_credentials";

    // Verified when the username does not exist so that response time does not reveal valid usernames.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash("dummy-password-for-timing"));

    public async Task<TokenResponse> IssueTokenAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        var (username, secret, allowedRoles) = ParseGrant(request);

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Username == username, cancellationToken);

        var passwordOk = passwordHasher.Verify(secret, user?.PasswordHash ?? _dummyHash.Value);
        if (user is null || !user.IsActive || !allowedRoles.Contains(user.Role) || !passwordOk)
        {
            // Same response for unknown user, wrong password, wrong grant type for the role, or disabled user.
            throw new ApiException(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials,
                "Invalid credentials", "The supplied credentials are not valid.");
        }

        return new TokenResponse
        {
            AccessToken = tokenIssuer.Issue(user),
            ExpiresIn = tokenIssuer.LifetimeSeconds,
            Role = user.Role,
            StudentNo = user.Role == Roles.Student ? user.StudentNo : null,
        };
    }

    private static (string Username, string Secret, string[] AllowedRoles) ParseGrant(TokenRequest request)
    {
        switch (request.GrantType?.Trim().ToLowerInvariant())
        {
            case PasswordGrant:
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    throw ApiException.Validation("The password grant requires 'username' and 'password'.");
                }

                return (request.Username.Trim(), request.Password, [Roles.Student, Roles.Admin]);

            case ClientCredentialsGrant:
                if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
                {
                    throw ApiException.Validation("The client_credentials grant requires 'clientId' and 'clientSecret'.");
                }

                return (request.ClientId.Trim(), request.ClientSecret, [Roles.Bank]);

            default:
                throw ApiException.Validation("'grantType' must be 'password' or 'client_credentials'.");
        }
    }
}
