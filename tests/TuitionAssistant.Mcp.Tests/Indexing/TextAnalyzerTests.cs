using TuitionAssistant.Mcp.Indexing;

namespace TuitionAssistant.Mcp.Tests.Indexing;

public class TextAnalyzerTests
{
    [Fact]
    public void Analyze_LowercasesRemovesStopWordsAndStems()
    {
        Assert.Equal(["instal", "deadlin"], TextAnalyzer.Analyze("The INSTALLMENTS and the deadlines"));
    }

    [Fact]
    public void Analyze_SingularAndPluralGiveSameTerm()
    {
        Assert.Equal(TextAnalyzer.Analyze("installment"), TextAnalyzer.Analyze("installments"));
    }

    [Fact]
    public void Analyze_StripsMarkdownAndKeepsNumbers()
    {
        Assert.Equal(["septemb", "30", "2026"], TextAnalyzer.Analyze("**September 30, 2026**"));
    }

    [Fact]
    public void Analyze_PercentageYieldsExactNumberAndGenericTerms()
    {
        Assert.Equal(["2%", "2", "%", "late", "fee"], TextAnalyzer.Analyze("2% late fee"));
    }

    [Fact]
    public void AnalyzeQuery_ExpandsSynonymsAndReturnsDistinctTerms()
    {
        var terms = TextAnalyzer.AnalyzeQuery("deadline deadline");

        Assert.Equal(["deadlin", "due"], terms);
    }

    [Fact]
    public void AnalyzeQuery_OnlyStopWords_ReturnsNoTerms()
    {
        Assert.Empty(TextAnalyzer.AnalyzeQuery("what is it"));
    }
}
