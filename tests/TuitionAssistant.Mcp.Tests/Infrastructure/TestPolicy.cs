using TuitionAssistant.Mcp.Indexing;

namespace TuitionAssistant.Mcp.Tests.Infrastructure;

/// <summary>Loads the real policy document shipped with the server.</summary>
public static class TestPolicy
{
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "University_Tuition_Policies.md");

    public static PolicyDocument Document { get; } = PolicyDocument.Load(Path);

    public static PolicyIndex CreateIndex(double minScore = 0.5) => new(Document, minScore);
}
