using System.Text.Json;
using ModelContextProtocol;
using TuitionAssistant.Mcp.Tests.Infrastructure;
using TuitionAssistant.Mcp.Tools;

namespace TuitionAssistant.Mcp.Tests.Tools;

/// <summary>The tool method called directly (no transport).</summary>
public class SearchToolTests
{
    private static readonly TuitionAssistant.Mcp.Indexing.PolicyIndex Index = TestPolicy.CreateIndex();

    [Fact]
    public void Search_ReturnsDocumentedJsonShape()
    {
        var json = TuitionPolicyTools.SearchTuitionPolicies(Index, "installment rules", 2);

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("University_Tuition_Policies.md", root.GetProperty("document").GetString());
        Assert.Equal("UNI-TUITION-2026-01", root.GetProperty("documentId").GetString());
        Assert.Equal("installment rules", root.GetProperty("query").GetString());
        Assert.Equal(2, root.GetProperty("totalMatches").GetInt32());
        var match = root.GetProperty("matches")[0];
        Assert.False(string.IsNullOrEmpty(match.GetProperty("section").GetString()));
        Assert.False(string.IsNullOrEmpty(match.GetProperty("heading").GetString()));
        Assert.False(string.IsNullOrEmpty(match.GetProperty("parentHeading").GetString()));
        Assert.False(string.IsNullOrEmpty(match.GetProperty("text").GetString()));
        Assert.True(match.GetProperty("score").GetDouble() > 0);
        Assert.False(root.TryGetProperty("message", out _));
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmptyListWithMessage()
    {
        var root = JsonDocument.Parse(TuitionPolicyTools.SearchTuitionPolicies(Index, "quantum chromodynamics")).RootElement;

        Assert.Equal(0, root.GetProperty("totalMatches").GetInt32());
        Assert.Empty(root.GetProperty("matches").EnumerateArray());
        Assert.Equal("No relevant policy section found.", root.GetProperty("message").GetString());
    }

    [Fact]
    public void Search_DefaultTopKIsThree()
    {
        var root = JsonDocument.Parse(TuitionPolicyTools.SearchTuitionPolicies(Index, "installment payment balance")).RootElement;

        Assert.Equal(3, root.GetProperty("totalMatches").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("x")]
    public void Search_TooShortQuery_IsAToolError(string query)
    {
        Assert.Throws<McpException>(() => TuitionPolicyTools.SearchTuitionPolicies(Index, query));
    }

    [Fact]
    public void Search_TooLongQuery_IsAToolError()
    {
        Assert.Throws<McpException>(() => TuitionPolicyTools.SearchTuitionPolicies(Index, new string('a', 301)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Search_TopKOutOfRange_IsAToolError(int topK)
    {
        Assert.Throws<McpException>(() => TuitionPolicyTools.SearchTuitionPolicies(Index, "installments", topK));
    }
}
