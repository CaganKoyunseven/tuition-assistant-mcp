using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TuitionAssistant.Mcp.Indexing;

/// <summary>The policy markdown file plus metadata parsed from its header.</summary>
public sealed partial class PolicyDocument
{
    public required string FileName { get; init; }

    public required string Text { get; init; }

    /// <summary>e.g. <c>UNI-TUITION-2026-01</c>.</summary>
    public required string DocumentId { get; init; }

    public string? Version { get; init; }

    public string? EffectiveYear { get; init; }

    /// <summary>SHA-256 of the file content (line endings normalized to LF).</summary>
    public required string Sha256 { get; init; }

    [GeneratedRegex(@"^\*\*Document ID:\*\*\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex DocumentIdPattern();

    [GeneratedRegex(@"^\*\*Version:\*\*\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^\*\*Effective academic year:\*\*\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex EffectiveYearPattern();

    public static PolicyDocument Load(string path) => Parse(Path.GetFileName(path), File.ReadAllText(path));

    public static PolicyDocument Parse(string fileName, string content)
    {
        // Normalize CRLF so a Windows checkout produces the same chunks and hash.
        var text = content.Replace("\r\n", "\n");
        var documentId = DocumentIdPattern().Match(text) is { Success: true } m
            ? m.Groups[1].Value
            : throw new InvalidOperationException($"'{fileName}' has no '**Document ID:**' header line.");

        return new PolicyDocument
        {
            FileName = fileName,
            Text = text,
            DocumentId = documentId,
            Version = VersionPattern().Match(text) is { Success: true } v ? v.Groups[1].Value : null,
            EffectiveYear = EffectiveYearPattern().Match(text) is { Success: true } y ? y.Groups[1].Value : null,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
        };
    }
}
