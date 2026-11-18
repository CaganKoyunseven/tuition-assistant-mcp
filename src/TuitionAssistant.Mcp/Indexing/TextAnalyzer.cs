using System.Text;
using System.Text.RegularExpressions;

namespace TuitionAssistant.Mcp.Indexing;

/// <summary>
/// Turns text into index terms: Unicode NFKC + lower case, markdown symbols removed,
/// split on non-alphanumeric characters, English stop words removed, Porter-stemmed.
/// Digits are kept (amounts and dates such as 300, 14, 2026); "2%" yields both "2%" and "2".
/// The same analyzer is used for documents and queries; queries additionally get a small synonym expansion.
/// </summary>
public static partial class TextAnalyzer
{
    [GeneratedRegex(@"[\p{L}\p{N}]+%?")]
    private static partial Regex TokenPattern();

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "as", "at", "be", "been", "but", "by", "can", "could", "did", "do", "does",
        "for", "from", "had", "has", "have", "how", "i", "if", "in", "into", "is", "it", "its", "me", "my",
        "of", "on", "or", "our", "so", "such", "than", "that", "the", "their", "them", "then", "there", "these",
        "this", "those", "to", "was", "we", "were", "what", "when", "where", "which", "who", "why", "will",
        "with", "would", "you", "your", "about", "any", "all", "also", "should", "may", "must", "not", "no",
        "s", "t", "up", "per", "many", "much",
    ];

    /// <summary>
    /// Query-time synonym expansion (documented in the README). Keys are lower-case surface words;
    /// values are extra words added to the query before analysis.
    /// </summary>
    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["deadline"] = ["due"],
        ["deadlines"] = ["due"],
        // British spelling only. ("installment → plan" was dropped: "plan" occurs only in §3.4 of the document,
        // so the expansion pulled every installment question towards that one section.)
        ["instalment"] = ["installment"],
        ["instalments"] = ["installment"],
        ["fee"] = ["late"],
        ["fees"] = ["late"],
        ["penalty"] = ["fee", "late"],
        ["refund"] = ["withdrawal"],
        ["refunds"] = ["withdrawal"],
        ["scholarship"] = ["merit"],
        ["scholarships"] = ["merit"],
        ["pay"] = ["payment"],
        ["paying"] = ["payment"],
        ["split"] = ["installment"],
        ["percentage"] = ["%"],
        ["percent"] = ["%"],
    };

    /// <summary>Analyzes document text into terms (duplicates kept: they are term frequencies).</summary>
    public static List<string> Analyze(string text)
    {
        var terms = new List<string>();
        foreach (var token in Tokenize(text))
        {
            AddToken(terms, token);
        }

        return terms;
    }

    /// <summary>Analyzes a query: synonym expansion, then the same pipeline as documents; distinct terms.</summary>
    public static List<string> AnalyzeQuery(string query)
    {
        var tokens = Tokenize(query).ToList();
        var expanded = new List<string>(tokens);
        foreach (var token in tokens)
        {
            if (Synonyms.TryGetValue(token, out var extra))
            {
                expanded.AddRange(extra);
            }
        }

        var terms = new List<string>();
        foreach (var token in expanded)
        {
            AddToken(terms, token);
        }

        return terms.Distinct().ToList();
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        // A bare "%" (from synonym expansion) is matched as its own token.
        if (normalized == "%")
        {
            yield return "%";
            yield break;
        }

        foreach (Match match in TokenPattern().Matches(normalized))
        {
            yield return match.Value;
        }
    }

    private static void AddToken(List<string> terms, string token)
    {
        if (token == "%")
        {
            terms.Add("%");
            return;
        }

        if (token.EndsWith('%'))
        {
            // "2%" → "2%" (exact percentage) + "2" (the number) + "%" (any percentage)
            terms.Add(token);
            terms.Add(token[..^1]);
            terms.Add("%");
            return;
        }

        if (StopWords.Contains(token))
        {
            return;
        }

        terms.Add(PorterStemmer.Stem(token));
    }
}
