using TuitionAssistant.Mcp.Indexing;
using TuitionAssistant.Mcp.Tests.Infrastructure;

namespace TuitionAssistant.Mcp.Tests.Indexing;

public class MarkdownChunkerTests
{
    private static readonly IReadOnlyList<PolicyChunk> Chunks = MarkdownChunker.Chunk(TestPolicy.Document);

    [Fact]
    public void Chunk_ProducesExactSectionList()
    {
        string[] expected =
        [
            "0", "1",
            "2.1", "2.2", "2.3",
            "3.1", "3.2", "3.3", "3.4",
            "4.1", "4.2", "4.3",
            "5.1", "5.2",
            "6.1", "6.2", "6.3",
            "7.1", "7.2", "7.3", "7.4",
            "8.1", "8.2", "8.3", "8.4",
            "9",
        ];

        Assert.Equal(expected, Chunks.Select(c => c.Section));
    }

    [Fact]
    public void Chunk_SubsectionHasHeadingAndParent()
    {
        var chunk = Chunks.Single(c => c.Section == "3.2");

        Assert.Equal("sec-3.2", chunk.ChunkId);
        Assert.Equal("Number and size of installments", chunk.Heading);
        Assert.Equal("3", chunk.ParentSection);
        Assert.Equal("Installment policy", chunk.ParentHeading);
        Assert.StartsWith("An approved balance may be divided into **up to three installments**.", chunk.Text);
        Assert.Equal("University_Tuition_Policies.md", chunk.Document);
        Assert.Equal("UNI-TUITION-2026-01", chunk.DocumentId);
    }

    [Fact]
    public void Chunk_SectionsWithoutSubsectionsAreSingleChunks()
    {
        var scope = Chunks.Single(c => c.Section == "1");
        Assert.Equal("Scope and terminology", scope.Heading);
        Assert.Null(scope.ParentSection);

        var guidance = Chunks.Single(c => c.Section == "9");
        Assert.Equal("Guidance for AI/MCP implementation", guidance.Heading);
        Assert.Contains("```json", guidance.Text); // the code block stays inside chunk 9
        Assert.Contains("REST API requests must go through the required API Gateway.", guidance.Text);
    }

    [Fact]
    public void Chunk_IntroBlockquoteBecomesScopeNotice()
    {
        var notice = Chunks[0];

        Assert.Equal("Document scope notice", notice.Heading);
        Assert.StartsWith("This document contains general university policies.", notice.Text);
    }

    [Fact]
    public void Chunk_NoChunkContainsAnotherHeading()
    {
        Assert.All(Chunks, c => Assert.DoesNotContain("\n## ", "\n" + c.Text));
        Assert.All(Chunks, c => Assert.DoesNotContain("\n### ", "\n" + c.Text));
    }

    [Fact]
    public void Document_HeaderMetadataIsParsed()
    {
        Assert.Equal("UNI-TUITION-2026-01", TestPolicy.Document.DocumentId);
        Assert.Equal("1.0", TestPolicy.Document.Version);
        Assert.Equal("2026–2027", TestPolicy.Document.EffectiveYear);
    }

    [Fact]
    public void Document_WindowsLineEndingsProduceSameChunks()
    {
        var crlf = PolicyDocument.Parse("University_Tuition_Policies.md", TestPolicy.Document.Text.Replace("\n", "\r\n"));

        Assert.Equal(TestPolicy.Document.Sha256, crlf.Sha256);
        Assert.Equal(Chunks.Select(c => c.Text), MarkdownChunker.Chunk(crlf).Select(c => c.Text));
    }
}
