using System.Diagnostics;

namespace TuitionAssistant.Mcp.Indexing;

/// <summary>Settings for the policy index (configuration section <c>Policy</c>).</summary>
public sealed class PolicyIndexOptions
{
    /// <summary>Path of the markdown file, relative to the application directory.</summary>
    public string DocumentPath { get; set; } = Path.Combine("Data", "University_Tuition_Policies.md");

    /// <summary>Matches scoring below this value are dropped (keeps irrelevant queries at 0 results).</summary>
    public double MinScore { get; set; } = 0.5;
}

/// <summary>
/// The pre-built policy index. Registered as a singleton and built ONCE when the server starts
/// (before it accepts MCP traffic). Searches only read it; nothing is re-indexed per question.
/// </summary>
public sealed class PolicyIndex
{
    private readonly Bm25Index _bm25;
    private readonly double _minScore;

    public PolicyIndex(PolicyDocument document, double minScore)
    {
        var stopwatch = Stopwatch.StartNew();
        Document = document;
        Chunks = MarkdownChunker.Chunk(document);
        _bm25 = new Bm25Index(Chunks);
        _minScore = minScore;
        BuildCount++;
        BuildDuration = stopwatch.Elapsed;
        IndexedAtUtc = DateTime.UtcNow;
    }

    public PolicyDocument Document { get; }

    public IReadOnlyList<PolicyChunk> Chunks { get; }

    public DateTime IndexedAtUtc { get; }

    public TimeSpan BuildDuration { get; }

    /// <summary>How many times this instance built its index (always 1; asserted by the tests).</summary>
    public int BuildCount { get; private set; }

    public IReadOnlyList<(PolicyChunk Chunk, double Score)> Search(string query, int topK) =>
        _bm25.Search(query, topK, _minScore);
}
