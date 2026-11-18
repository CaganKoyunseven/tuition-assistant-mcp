using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TuitionAssistant.Mcp.Indexing;

namespace TuitionAssistant.Mcp.Tools;

/// <summary>MCP tools exposed by this server.</summary>
[McpServerToolType]
public sealed class TuitionPolicyTools
{
    public const string SearchToolName = "search_tuition_policies";
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 300;
    public const int MaxTopK = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Searches the pre-built policy index. Invalid input is reported to the caller as a tool error
    /// (<see cref="McpException"/> becomes a result with <c>isError: true</c>) instead of crashing.
    /// </summary>
    [McpServerTool(Name = SearchToolName, Title = "Search tuition policies", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Searches the official University Tuition Policies document (UNI-TUITION-2026-01) and returns the most relevant policy excerpts with section references. Use for general rules: deadlines, installments, late fees, scholarships, refunds, banking/payment instructions. It does NOT contain any student's balance or payments.")]
    public static string SearchTuitionPolicies(
        PolicyIndex index,
        [Description("Natural-language search query in English, e.g. 'installment eligibility rules' (2-300 characters).")]
        [StringLength(MaxQueryLength, MinimumLength = MinQueryLength)] string query,
        [Description("Number of excerpts to return (1-5, default 3).")]
        [Range(1, MaxTopK)] int top_k = 3)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length is < MinQueryLength or > MaxQueryLength)
        {
            throw new McpException($"'query' must be between {MinQueryLength} and {MaxQueryLength} characters.");
        }

        if (top_k is < 1 or > MaxTopK)
        {
            throw new McpException($"'top_k' must be between 1 and {MaxTopK}.");
        }

        var matches = index.Search(trimmed, top_k)
            .Select(m => new PolicyMatch
            {
                Section = m.Chunk.Section,
                Heading = m.Chunk.Heading,
                ParentHeading = m.Chunk.ParentHeading,
                Text = m.Chunk.Text,
                Score = Math.Round(m.Score, 2),
            })
            .ToList();

        var result = new PolicySearchResult
        {
            Document = index.Document.FileName,
            DocumentId = index.Document.DocumentId,
            Query = trimmed,
            TotalMatches = matches.Count,
            Matches = matches,
            Message = matches.Count == 0 ? "No relevant policy section found." : null,
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
