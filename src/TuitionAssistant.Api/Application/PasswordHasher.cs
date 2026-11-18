using Microsoft.Extensions.Options;

namespace TuitionAssistant.Api.Application;

/// <summary>Hashes and verifies passwords and client secrets.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>Options for <see cref="BcryptPasswordHasher"/> (configuration section <c>Auth</c>).</summary>
public class PasswordHashingOptions
{
    /// <summary>BCrypt cost factor. 11 in production; tests use a lower value for speed.</summary>
    public int BcryptWorkFactor { get; set; } = 11;
}

/// <summary>BCrypt implementation: salted, deliberately slow hashing.</summary>
public class BcryptPasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, options.Value.BcryptWorkFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
