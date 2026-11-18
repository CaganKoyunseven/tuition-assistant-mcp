using System.Security.Cryptography;

namespace TuitionAssistant.Api.Tests.Infrastructure;

/// <summary>RSA key pairs generated once per test run; no key material is stored in the repository.</summary>
public static class TestKeys
{
    private static readonly Lazy<RSA> PrimaryKey = new(() => RSA.Create(2048));
    private static readonly Lazy<RSA> OtherKey = new(() => RSA.Create(2048));

    /// <summary>The key the API under test is configured with.</summary>
    public static string PrivateKeyPem => PrimaryKey.Value.ExportPkcs8PrivateKeyPem();

    /// <summary>An unrelated key, for "token signed by someone else" tests.</summary>
    public static RSA Other => OtherKey.Value;
}
