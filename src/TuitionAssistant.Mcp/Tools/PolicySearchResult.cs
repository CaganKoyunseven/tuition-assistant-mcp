namespace TuitionAssistant.Mcp.Tools;

/// <summary>JSON returned by <c>search_tuition_policies</c> (extends the format suggested in policy §9).</summary>
public sealed class PolicySearchResult
{
    public required string Document { get; init; }

    public required string DocumentId { get; init; }

    public required string Query { get; init; }

    public int TotalMatches { get; init; }

    public required IReadOnlyList<PolicyMatch> Matches { get; init; }

    /// <summary>Set when nothing relevant was found.</summary>
    public string? Message { get; init; }
}

/// <summary>One relevant policy excerpt with its section reference.</summary>
public sealed class PolicyMatch
{
    /// <summary>Section number to cite, e.g. <c>3.2</c>.</summary>
    public required string Section { get; init; }

    public required string Heading { get; init; }

    public string? ParentHeading { get; init; }

    public required string Text { get; init; }

    /// <summary>BM25 relevance score (higher is more relevant).</summary>
    public double Score { get; init; }
}
