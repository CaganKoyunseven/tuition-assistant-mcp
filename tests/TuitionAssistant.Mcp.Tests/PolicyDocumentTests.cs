using System.Security.Cryptography;

namespace TuitionAssistant.Mcp.Tests;

/// <summary>
/// Guards the policy document: it must ship with the MCP server and must stay byte-for-byte unchanged.
/// </summary>
public class PolicyDocumentTests
{
    // SHA-256 of the original University_Tuition_Policies.md provided with the assignment.
    private const string OriginalSha256 = "01eb390e5cee7329f7269a19156942e3a6fc07c01d36adc7d22e1d012b3c39c2";

    private static string PolicyPath =>
        Path.Combine(AppContext.BaseDirectory, "Data", "University_Tuition_Policies.md");

    [Fact]
    public void PolicyDocument_IsCopiedToOutput()
    {
        Assert.True(File.Exists(PolicyPath), $"Policy document not found at {PolicyPath}");
    }

    [Fact]
    public void PolicyDocument_IsUnchanged()
    {
        // Normalize CRLF so a Windows checkout (core.autocrlf) does not produce a false failure.
        var text = File.ReadAllText(PolicyPath).Replace("\r\n", "\n");
        var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

        Assert.Equal(OriginalSha256, hash);
    }

    [Fact]
    public void PolicyDocument_ContainsExpectedDocumentId()
    {
        Assert.Contains("**Document ID:** UNI-TUITION-2026-01", File.ReadAllText(PolicyPath));
    }
}
