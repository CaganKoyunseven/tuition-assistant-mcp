using TuitionAssistant.Mcp.Indexing;
using TuitionAssistant.Mcp.Tests.Infrastructure;

namespace TuitionAssistant.Mcp.Tests.Indexing;

/// <summary>The retrieval demonstrations required by the assignment (README "MCP retrieval tests").</summary>
public class PolicyRetrievalTests
{
    private static readonly PolicyIndex Index = TestPolicy.CreateIndex();

    [Theory]
    [InlineData("payment deadline fall term", "2.1")]
    [InlineData("how many installments can I split my balance into", "3.2")]
    [InlineData("refund if I withdraw early", "6.1")]
    [InlineData("late fee percentage", "4.1")]
    [InlineData("installment eligibility rules", "3.1")]
    [InlineData("merit scholarship", "5.1")]
    [InlineData("when is the spring deadline", "2.2")]
    public void Search_ReturnsExpectedTopSection(string query, string expectedSection)
    {
        var results = Index.Search(query, 3);

        Assert.NotEmpty(results);
        Assert.Equal(expectedSection, results[0].Chunk.Section);
    }

    [Theory]
    [InlineData("quantum chromodynamics")]
    [InlineData("pizza recipe")]
    public void Search_IrrelevantQuery_ReturnsNoMatches(string query)
    {
        Assert.Empty(Index.Search(query, 5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Search_RespectsTopK(int topK)
    {
        Assert.Equal(topK, Index.Search("installment payment balance", topK).Count);
    }

    [Fact]
    public void Search_ResultsAreOrderedByScore()
    {
        var scores = Index.Search("payment balance installment deadline", 5).Select(r => r.Score).ToList();

        Assert.Equal(scores.OrderByDescending(s => s), scores);
    }

    [Fact]
    public void Index_IsBuiltOnce_AcrossManyQueries()
    {
        var index = TestPolicy.CreateIndex();
        var builtAt = index.IndexedAtUtc;

        for (var i = 0; i < 100; i++)
        {
            index.Search($"installment query {i}", 3);
        }

        Assert.Equal(1, index.BuildCount);
        Assert.Equal(builtAt, index.IndexedAtUtc);
    }
}
