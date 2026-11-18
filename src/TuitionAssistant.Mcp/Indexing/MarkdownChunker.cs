using System.Text;
using System.Text.RegularExpressions;

namespace TuitionAssistant.Mcp.Indexing;

/// <summary>
/// Splits the policy markdown into section-level chunks:
/// <list type="bullet">
/// <item>one chunk per <c>###</c> subsection (e.g. <c>3.2 Number and size of installments</c>);</item>
/// <item>a <c>##</c> section without subsections becomes one chunk (sections 1 and 9);</item>
/// <item>the introductory blockquote becomes chunk <c>0</c> ("Document scope notice").</item>
/// </list>
/// Fenced code blocks are kept inside their chunk and never treated as headings.
/// </summary>
public static partial class MarkdownChunker
{
    [GeneratedRegex(@"^(#{2,3})\s+(\d+(?:\.\d+)?)\.?\s+(.+?)\s*$")]
    private static partial Regex HeadingPattern();

    private sealed class Builder(string section, string heading, string? parentSection, string? parentHeading)
    {
        public string Section { get; } = section;
        public string Heading { get; } = heading;
        public string? ParentSection { get; } = parentSection;
        public string? ParentHeading { get; } = parentHeading;
        public StringBuilder Body { get; } = new();
    }

    public static IReadOnlyList<PolicyChunk> Chunk(PolicyDocument document)
    {
        var chunks = new List<PolicyChunk>();
        Builder? current = null;
        (string Section, string Heading)? currentSection = null;
        var sectionHasSubsections = false;
        Builder? pendingSectionChunk = null;
        var intro = new StringBuilder();
        var inCodeBlock = false;
        var seenFirstSection = false;

        void Flush(Builder? builder)
        {
            if (builder is null)
            {
                return;
            }

            var text = builder.Body.ToString().Trim();
            if (text.Length == 0)
            {
                return;
            }

            chunks.Add(new PolicyChunk
            {
                ChunkId = $"sec-{builder.Section}",
                Section = builder.Section,
                Heading = builder.Heading,
                ParentSection = builder.ParentSection,
                ParentHeading = builder.ParentHeading,
                Document = document.FileName,
                DocumentId = document.DocumentId,
                Text = text,
            });
        }

        foreach (var line in document.Text.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
            }

            var match = inCodeBlock ? Match.Empty : HeadingPattern().Match(line);
            if (match.Success && match.Groups[1].Value == "##")
            {
                // New top-level section: close whatever was open.
                Flush(current);
                if (!sectionHasSubsections)
                {
                    Flush(pendingSectionChunk);
                }

                seenFirstSection = true;
                currentSection = (match.Groups[2].Value, match.Groups[3].Value);
                sectionHasSubsections = false;
                pendingSectionChunk = new Builder(currentSection.Value.Section, currentSection.Value.Heading, null, null);
                current = null;
                continue;
            }

            if (match.Success && match.Groups[1].Value == "###" && currentSection is { } parent)
            {
                Flush(current);
                sectionHasSubsections = true;
                current = new Builder(match.Groups[2].Value, match.Groups[3].Value, parent.Section, parent.Heading);
                continue;
            }

            if (!seenFirstSection)
            {
                // Before the first ## heading: only the blockquote is kept (the scope notice).
                if (line.StartsWith('>'))
                {
                    intro.AppendLine(line.TrimStart('>').Trim());
                }

                continue;
            }

            (current ?? pendingSectionChunk)?.Body.AppendLine(line);
        }

        Flush(current);
        if (!sectionHasSubsections)
        {
            Flush(pendingSectionChunk);
        }

        if (intro.Length > 0)
        {
            chunks.Insert(0, new PolicyChunk
            {
                ChunkId = "sec-0",
                Section = "0",
                Heading = "Document scope notice",
                Document = document.FileName,
                DocumentId = document.DocumentId,
                Text = intro.ToString().Trim(),
            });
        }

        return chunks;
    }
}
