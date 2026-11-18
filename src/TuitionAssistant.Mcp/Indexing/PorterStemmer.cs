namespace TuitionAssistant.Mcp.Indexing;

/// <summary>
/// The Porter (1980) stemming algorithm for English: reduces words to a common stem so that
/// "installment", "installments" and "instalment" style variants match
/// (e.g. "payments" → "payment", "withdrawal" → "withdraw").
/// Implemented here (no external dependency); expects lower-case ASCII words.
/// </summary>
public static class PorterStemmer
{
    public static string Stem(string word)
    {
        if (word.Length <= 2 || !word.All(c => c is >= 'a' and <= 'z'))
        {
            return word; // short words, numbers and non-ASCII tokens are left untouched
        }

        var s = new Stemmer(word);
        s.Step1Ab();
        if (s.K > 0)
        {
            s.Step1C();
            s.Step2();
            s.Step3();
            s.Step4();
            s.Step5();
        }

        return s.Result;
    }

    /// <summary>Classic buffer-based implementation: b[0..k] is the current word, j marks a suffix boundary.</summary>
    private sealed class Stemmer(string word)
    {
        private readonly char[] _b = word.ToCharArray();
        private int _j;

        public int K { get; private set; } = word.Length - 1;

        public string Result => new(_b, 0, K + 1);

        // True if b[i] is a consonant ('y' is a consonant when it follows a vowel or starts the word).
        private bool IsConsonant(int i) => _b[i] switch
        {
            'a' or 'e' or 'i' or 'o' or 'u' => false,
            'y' => i == 0 || !IsConsonant(i - 1),
            _ => true,
        };

        // m() = number of VC sequences in b[0..j]: <c><v> gives 0, <c>vc<v> gives 1, <c>vcvc<v> gives 2 ...
        private int Measure()
        {
            var n = 0;
            var i = 0;
            while (true)
            {
                if (i > _j) return n;
                if (!IsConsonant(i)) break;
                i++;
            }

            i++;
            while (true)
            {
                while (true)
                {
                    if (i > _j) return n;
                    if (IsConsonant(i)) break;
                    i++;
                }

                i++;
                n++;
                while (true)
                {
                    if (i > _j) return n;
                    if (!IsConsonant(i)) break;
                    i++;
                }

                i++;
            }
        }

        private bool VowelInStem()
        {
            for (var i = 0; i <= _j; i++)
            {
                if (!IsConsonant(i)) return true;
            }

            return false;
        }

        private bool DoubleConsonant(int j) => j >= 1 && _b[j] == _b[j - 1] && IsConsonant(j);

        // cvc(i): b[i-2..i] is consonant-vowel-consonant and the last consonant is not w, x or y (e.g. "hop").
        private bool Cvc(int i)
        {
            if (i < 2 || !IsConsonant(i) || IsConsonant(i - 1) || !IsConsonant(i - 2)) return false;
            return _b[i] is not ('w' or 'x' or 'y');
        }

        private bool Ends(string suffix)
        {
            var length = suffix.Length;
            if (length > K + 1) return false;
            for (var i = 0; i < length; i++)
            {
                if (_b[K - length + 1 + i] != suffix[i]) return false;
            }

            _j = K - length;
            return true;
        }

        // Replaces b[j+1..k] with s.
        private void SetTo(string s)
        {
            for (var i = 0; i < s.Length; i++)
            {
                _b[_j + 1 + i] = s[i];
            }

            K = _j + s.Length;
        }

        private void ReplaceIfMeasure(string s)
        {
            if (Measure() > 0) SetTo(s);
        }

        // Step 1ab: plurals and -ed / -ing (caresses → caress, ponies → poni, hopping → hop, agreed → agree).
        public void Step1Ab()
        {
            if (_b[K] == 's')
            {
                if (Ends("sses")) K -= 2;
                else if (Ends("ies")) SetTo("i");
                else if (K >= 1 && _b[K - 1] != 's') K--;
            }

            if (Ends("eed"))
            {
                if (Measure() > 0) K--;
            }
            else if ((Ends("ed") || Ends("ing")) && VowelInStem())
            {
                K = _j;
                if (Ends("at")) SetTo("ate");
                else if (Ends("bl")) SetTo("ble");
                else if (Ends("iz")) SetTo("ize");
                else if (DoubleConsonant(K))
                {
                    K--;
                    if (_b[K] is 'l' or 's' or 'z') K++;
                }
                else if (Measure() == 1 && Cvc(K)) { _j = K; SetTo("e"); }
            }
        }

        // Step 1c: terminal y → i when there is another vowel in the stem (happy → happi).
        public void Step1C()
        {
            if (Ends("y") && VowelInStem()) _b[K] = 'i';
        }

        // Step 2: double suffixes → single ones (relational → relate, conditional → condition).
        public void Step2()
        {
            if (K == 0) return;
            switch (_b[K - 1])
            {
                case 'a':
                    if (Ends("ational")) { ReplaceIfMeasure("ate"); break; }
                    if (Ends("tional")) { ReplaceIfMeasure("tion"); }
                    break;
                case 'c':
                    if (Ends("enci")) { ReplaceIfMeasure("ence"); break; }
                    if (Ends("anci")) { ReplaceIfMeasure("ance"); }
                    break;
                case 'e':
                    if (Ends("izer")) { ReplaceIfMeasure("ize"); }
                    break;
                case 'l':
                    if (Ends("bli")) { ReplaceIfMeasure("ble"); break; }
                    if (Ends("alli")) { ReplaceIfMeasure("al"); break; }
                    if (Ends("entli")) { ReplaceIfMeasure("ent"); break; }
                    if (Ends("eli")) { ReplaceIfMeasure("e"); break; }
                    if (Ends("ousli")) { ReplaceIfMeasure("ous"); }
                    break;
                case 'o':
                    if (Ends("ization")) { ReplaceIfMeasure("ize"); break; }
                    if (Ends("ation")) { ReplaceIfMeasure("ate"); break; }
                    if (Ends("ator")) { ReplaceIfMeasure("ate"); }
                    break;
                case 's':
                    if (Ends("alism")) { ReplaceIfMeasure("al"); break; }
                    if (Ends("iveness")) { ReplaceIfMeasure("ive"); break; }
                    if (Ends("fulness")) { ReplaceIfMeasure("ful"); break; }
                    if (Ends("ousness")) { ReplaceIfMeasure("ous"); }
                    break;
                case 't':
                    if (Ends("aliti")) { ReplaceIfMeasure("al"); break; }
                    if (Ends("iviti")) { ReplaceIfMeasure("ive"); break; }
                    if (Ends("biliti")) { ReplaceIfMeasure("ble"); }
                    break;
                case 'g':
                    if (Ends("logi")) { ReplaceIfMeasure("log"); }
                    break;
            }
        }

        // Step 3: -ic-, -full, -ness etc. (triplicate → triplic, hopeful → hope, goodness → good).
        public void Step3()
        {
            switch (_b[K])
            {
                case 'e':
                    if (Ends("icate")) { ReplaceIfMeasure("ic"); break; }
                    if (Ends("ative")) { ReplaceIfMeasure(""); break; }
                    if (Ends("alize")) { ReplaceIfMeasure("al"); }
                    break;
                case 'i':
                    if (Ends("iciti")) { ReplaceIfMeasure("ic"); }
                    break;
                case 'l':
                    if (Ends("ical")) { ReplaceIfMeasure("ic"); break; }
                    if (Ends("ful")) { ReplaceIfMeasure(""); }
                    break;
                case 's':
                    if (Ends("ness")) { ReplaceIfMeasure(""); }
                    break;
            }
        }

        // Step 4: removes -ant, -ence etc. when m > 1 (revival → reviv, adjustment → adjust).
        public void Step4()
        {
            if (K == 0) return;
            var matched = _b[K - 1] switch
            {
                'a' => Ends("al"),
                'c' => Ends("ance") || Ends("ence"),
                'e' => Ends("er"),
                'i' => Ends("ic"),
                'l' => Ends("able") || Ends("ible"),
                'n' => Ends("ant") || Ends("ement") || Ends("ment") || Ends("ent"),
                'o' => (Ends("ion") && _j >= 0 && _b[_j] is 's' or 't') || Ends("ou"),
                's' => Ends("ism"),
                't' => Ends("ate") || Ends("iti"),
                'u' => Ends("ous"),
                'v' => Ends("ive"),
                'z' => Ends("ize"),
                _ => false,
            };

            if (matched && Measure() > 1) K = _j;
        }

        // Step 5: removes a final -e and reduces -ll when m > 1 (probate → probat, controll → control).
        public void Step5()
        {
            _j = K;
            if (_b[K] == 'e')
            {
                var m = Measure();
                if (m > 1 || (m == 1 && !Cvc(K - 1))) K--;
            }

            if (_b[K] == 'l' && DoubleConsonant(K) && Measure() > 1) K--;
        }
    }
}
