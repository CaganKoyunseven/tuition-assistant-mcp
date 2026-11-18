namespace TuitionAssistant.Mcp.Indexing;

/// <summary>One searchable unit of the policy document (a ### subsection, or a ## section without subsections).</summary>
public sealed record PolicyChunk
{
    /// <summary>Stable id, e.g. <c>sec-3.2</c>.</summary>
    public required string ChunkId { get; init; }

    /// <summary>Section reference used in citations, e.g. <c>3.2</c>.</summary>
    public required string Section { get; init; }

    /// <summary>Heading text without the number, e.g. <c>Number and size of installments</c>.</summary>
    public required string Heading { get; init; }

    /// <summary>Parent section number (e.g. <c>3</c>); null for top-level chunks.</summary>
    public string? ParentSection { get; init; }

    /// <summary>Parent heading (e.g. <c>Installment policy</c>); null for top-level chunks.</summary>
    public string? ParentHeading { get; init; }

    public required string Document { get; init; }

    public required string DocumentId { get; init; }

    /// <summary>Raw markdown body of the chunk (shown to the user/LLM).</summary>
    public required string Text { get; init; }
}
