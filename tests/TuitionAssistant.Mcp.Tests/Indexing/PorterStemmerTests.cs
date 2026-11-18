using TuitionAssistant.Mcp.Indexing;

namespace TuitionAssistant.Mcp.Tests.Indexing;

/// <summary>Reference outputs from Martin Porter's published examples and test vocabulary.</summary>
public class PorterStemmerTests
{
    [Theory]
    [InlineData("caresses", "caress")]
    [InlineData("ponies", "poni")]
    [InlineData("ties", "ti")]
    [InlineData("caress", "caress")]
    [InlineData("cats", "cat")]
    [InlineData("feed", "feed")]
    [InlineData("agreed", "agre")]
    [InlineData("plastered", "plaster")]
    [InlineData("motoring", "motor")]
    [InlineData("sing", "sing")]
    [InlineData("conflated", "conflat")]
    [InlineData("troubled", "troubl")]
    [InlineData("sized", "size")]
    [InlineData("hopping", "hop")]
    [InlineData("tanned", "tan")]
    [InlineData("falling", "fall")]
    [InlineData("hissing", "hiss")]
    [InlineData("fizzed", "fizz")]
    [InlineData("failing", "fail")]
    [InlineData("filing", "file")]
    [InlineData("happy", "happi")]
    [InlineData("sky", "sky")]
    [InlineData("relational", "relat")]
    [InlineData("conditional", "condit")]
    [InlineData("rational", "ration")]
    [InlineData("generalization", "gener")]
    [InlineData("hopeful", "hope")]
    [InlineData("goodness", "good")]
    [InlineData("revival", "reviv")]
    [InlineData("allowance", "allow")]
    [InlineData("adjustment", "adjust")]
    [InlineData("probate", "probat")]
    [InlineData("rate", "rate")]
    [InlineData("cease", "ceas")]
    [InlineData("controlling", "control")]
    [InlineData("generously", "gener")]
    public void Stem_MatchesReferenceOutput(string word, string expected)
    {
        Assert.Equal(expected, PorterStemmer.Stem(word));
    }

    [Theory]
    [InlineData("installment", "installments")]
    [InlineData("payment", "payments")]
    [InlineData("withdraw", "withdrawal")]
    [InlineData("withdraw", "withdraws")]
    [InlineData("refund", "refunds")]
    [InlineData("deadline", "deadlines")]
    public void Stem_PolicyVocabularyVariantsShareAStem(string a, string b)
    {
        Assert.Equal(PorterStemmer.Stem(a), PorterStemmer.Stem(b));
    }

    [Theory]
    [InlineData("2026")]
    [InlineData("is")]
    [InlineData("ödeme")]
    public void Stem_NumbersShortAndNonAsciiWords_AreUnchanged(string word)
    {
        Assert.Equal(word, PorterStemmer.Stem(word));
    }
}
