namespace TuitionAssistant.Mcp.Indexing;

/// <summary>
/// In-memory Okapi BM25 full-text index over policy chunks.
/// Score(q, d) = Σ over query terms t of IDF(t) · tf(t,d)·(k1+1) / (tf(t,d) + k1·(1 − b + b·|d|/avgdl)),
/// with IDF(t) = ln(1 + (N − df + 0.5) / (df + 0.5)), k1 = 1.2, b = 0.75.
/// Heading terms are indexed twice (field boost), the parent heading once, the body once.
/// </summary>
public sealed class Bm25Index
{
    public const double K1 = 1.2;
    public const double B = 0.75;

    private readonly IReadOnlyList<PolicyChunk> _chunks;
    private readonly Dictionary<string, int>[] _termFrequencies;
    private readonly int[] _lengths;
    private readonly Dictionary<string, int> _documentFrequencies = new();
    private readonly double _averageLength;

    public Bm25Index(IReadOnlyList<PolicyChunk> chunks)
    {
        _chunks = chunks;
        _termFrequencies = new Dictionary<string, int>[chunks.Count];
        _lengths = new int[chunks.Count];

        for (var i = 0; i < chunks.Count; i++)
        {
            var terms = IndexTerms(chunks[i]);
            _lengths[i] = terms.Count;
            _termFrequencies[i] = terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            foreach (var term in _termFrequencies[i].Keys)
            {
                _documentFrequencies[term] = _documentFrequencies.GetValueOrDefault(term) + 1;
            }
        }

        _averageLength = chunks.Count == 0 ? 0 : _lengths.Average();
    }

    public int Count => _chunks.Count;

    /// <summary>Returns chunks scoring at least <paramref name="minScore"/>, best first (ties: lower section number first).</summary>
    public IReadOnlyList<(PolicyChunk Chunk, double Score)> Search(string query, int topK, double minScore)
    {
        var queryTerms = TextAnalyzer.AnalyzeQuery(query);
        if (queryTerms.Count == 0)
        {
            return [];
        }

        var results = new List<(PolicyChunk, double)>();
        for (var i = 0; i < _chunks.Count; i++)
        {
            var score = 0.0;
            foreach (var term in queryTerms)
            {
                if (!_termFrequencies[i].TryGetValue(term, out var tf))
                {
                    continue;
                }

                var df = _documentFrequencies[term];
                var idf = Math.Log(1 + ((_chunks.Count - df + 0.5) / (df + 0.5)));
                var norm = K1 * (1 - B + (B * _lengths[i] / _averageLength));
                score += idf * (tf * (K1 + 1)) / (tf + norm);
            }

            if (score >= minScore)
            {
                results.Add((_chunks[i], score));
            }
        }

        return results
            .OrderByDescending(r => r.Item2)
            .ThenBy(r => SectionSortKey(r.Item1.Section))
            .Take(topK)
            .ToList();
    }

    private static List<string> IndexTerms(PolicyChunk chunk)
    {
        var headingTerms = TextAnalyzer.Analyze(chunk.Heading);
        var terms = new List<string>(headingTerms);
        terms.AddRange(headingTerms); // heading counted ×2
        if (chunk.ParentHeading is not null)
        {
            terms.AddRange(TextAnalyzer.Analyze(chunk.ParentHeading));
        }

        terms.AddRange(TextAnalyzer.Analyze(chunk.Text));
        return terms;
    }

    private static (int Major, int Minor) SectionSortKey(string section)
    {
        var parts = section.Split('.');
        return (int.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 0);
    }
}
