using System.Text.RegularExpressions;

namespace Maise.Core.Text;

public static class TextNormalizer
{
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mrs"] = "misess",
        ["mr"] = "mister",
        ["dr"] = "doctor",
        ["st"] = "saint",
        ["co"] = "company",
        ["jr"] = "junior",
        ["maj"] = "major",
        ["gen"] = "general",
        ["drs"] = "doctors",
        ["rev"] = "reverend",
        ["lt"] = "lieutenant",
        ["hon"] = "honorable",
        ["sgt"] = "sergeant",
        ["capt"] = "captain",
        ["esq"] = "esquire",
        ["ltd"] = "limited",
        ["col"] = "colonel",
        ["ft"] = "foot",
        ["pty"] = "proprietary",
        ["vs"] = "versus",
        ["approx"] = "approximately",
        ["dept"] = "department",
        ["prof"] = "professor",
        ["etc"] = "et cetera",
        ["eg"] = "for example",
        ["ie"] = "that is",
    };

    private static readonly string[] Ones =
    [
        "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
        "seventeen", "eighteen", "nineteen"
    ];

    private static readonly string[] Tens =
    [
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
    ];

    private static readonly string[] Scales =
    [
        "thousand", "million", "billion", "trillion", "quadrillion", "quintillion"
    ];

    private static readonly string[] OrdinalOnes =
    [
        "", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth",
        "tenth", "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth",
        "seventeenth", "eighteenth", "nineteenth"
    ];

    private static readonly string[] OrdinalTens =
    [
        "", "", "twentieth", "thirtieth", "fortieth", "fiftieth",
        "sixtieth", "seventieth", "eightieth", "ninetieth"
    ];

    private static readonly Regex OrdinalRegex = new(@"\b(\d[\d,]*)(st|nd|rd|th)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"-?\d[\d,]*(?:\.\d+)?(?![a-zA-Z])", RegexOptions.Compiled);
    private static readonly Regex AbbrevRegex = new(@"\b([A-Za-z]+)\.?(?=[\s,;:!?]|$)", RegexOptions.Compiled);
    private static readonly Regex EgRegex = new(@"\be\.g\.?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IeRegex = new(@"\bi\.e\.?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EtcRegex = new(@"\betc\.?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SentenceSplitRegex = new(@"(?<=[.!?])(?:\s+|$)", RegexOptions.Compiled);

    public static string HundredsToWords(int n)
    {
        if (n == 0) return "";
        var sb = new System.Text.StringBuilder();
        if (n >= 100)
        {
            sb.Append(Ones[n / 100]).Append(" hundred");
            if (n % 100 != 0) sb.Append(" and ");
        }
        var rem = n % 100;
        if (rem > 0)
        {
            if (rem < 20)
            {
                sb.Append(Ones[rem]);
            }
            else
            {
                sb.Append(Tens[rem / 10]);
                if (rem % 10 != 0)
                {
                    sb.Append(' ').Append(Ones[rem % 10]);
                }
            }
        }
        return sb.ToString().Trim();
    }

    public static string LongToWords(long n)
    {
        if (n == 0L) return "zero";
        if (n < 0L) return $"minus {LongToWords(-n)}";

        var groups = new List<int>();
        var rem = n;
        while (rem > 0)
        {
            groups.Insert(0, (int)(rem % 1000));
            rem /= 1000;
        }

        var parts = new List<string>();
        for (var i = 0; i < groups.Count; i++)
        {
            var chunk = groups[i];
            if (chunk == 0) continue;
            var words = HundredsToWords(chunk);
            var scaleIdx = groups.Count - i - 2;
            parts.Add(scaleIdx >= 0 && scaleIdx < Scales.Length ? $"{words} {Scales[scaleIdx]}" : words);
        }
        return string.Join(" ", parts);
    }

    private static string DigitToWord(char c) => c switch
    {
        '0' => "zero",
        '1' => "one",
        '2' => "two",
        '3' => "three",
        '4' => "four",
        '5' => "five",
        '6' => "six",
        '7' => "seven",
        '8' => "eight",
        '9' => "nine",
        _ => c.ToString()
    };

    public static string HundredsToOrdinal(int n)
    {
        if (n == 0) return "";
        var sb = new System.Text.StringBuilder();
        if (n >= 100)
        {
            if (n % 100 == 0)
            {
                sb.Append(Ones[n / 100]).Append(" hundredth");
                return sb.ToString().Trim();
            }
            sb.Append(Ones[n / 100]).Append(" hundred and ");
        }
        var rem = n % 100;
        if (rem < 20)
        {
            sb.Append(OrdinalOnes[rem]);
        }
        else if (rem % 10 == 0)
        {
            sb.Append(OrdinalTens[rem / 10]);
        }
        else
        {
            sb.Append(Tens[rem / 10]).Append(' ').Append(OrdinalOnes[rem % 10]);
        }
        return sb.ToString().Trim();
    }

    public static string LongToOrdinal(long n)
    {
        if (n == 0L) return "zeroth";
        if (n < 0L) return $"minus {LongToOrdinal(-n)}";

        var groups = new List<int>();
        var rem = n;
        while (rem > 0)
        {
            groups.Insert(0, (int)(rem % 1000));
            rem /= 1000;
        }

        var nonZeroCount = groups.Count(g => g != 0);
        if (nonZeroCount > 1)
        {
            var parts = new List<string>();
            var lastNonZeroIdx = -1;
            for (var i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i] != 0) { lastNonZeroIdx = i; break; }
            }

            for (var i = 0; i < groups.Count; i++)
            {
                var chunk = groups[i];
                if (chunk == 0) continue;
                var scaleIdx = groups.Count - i - 2;
                var isLast = (i == lastNonZeroIdx);
                if (isLast)
                {
                    var words = HundredsToOrdinal(chunk);
                    parts.Add(scaleIdx >= 0 && scaleIdx < Scales.Length ? $"{words} {Scales[scaleIdx]}" : words);
                }
                else
                {
                    var words = HundredsToWords(chunk);
                    parts.Add(scaleIdx >= 0 && scaleIdx < Scales.Length ? $"{words} {Scales[scaleIdx]}" : words);
                }
            }
            return string.Join(" ", parts);
        }
        return HundredsToOrdinal(groups.Count > 0 ? groups[0] : 0);
    }

    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var result = text;

        // 1. Dotted multi-character abbreviations
        result = EgRegex.Replace(result, "for example");
        result = IeRegex.Replace(result, "that is");
        result = EtcRegex.Replace(result, "et cetera");

        // 2. Ordinal numbers -> words
        result = OrdinalRegex.Replace(result, match =>
        {
            var raw = match.Groups[1].Value.Replace(",", "");
            return long.TryParse(raw, out var n) ? LongToOrdinal(n) : match.Value;
        });

        // 3. Numbers -> words
        result = NumberRegex.Replace(result, match =>
        {
            var raw = match.Value.Replace(",", "");
            var dotIdx = raw.IndexOf('.');
            if (dotIdx >= 0)
            {
                var isNeg = raw.StartsWith('-');
                var absStr = raw.Substring(isNeg ? 1 : 0, dotIdx - (isNeg ? 1 : 0));
                if (!long.TryParse(absStr, out var absInt)) return match.Value;
                var intWords = isNeg ? $"minus {LongToWords(absInt)}" : LongToWords(absInt);
                var decWords = string.Join(" ", raw.Substring(dotIdx + 1).Select(DigitToWord));
                return $"{intWords} point {decWords}";
            }
            return long.TryParse(raw, out var val) ? LongToWords(val) : match.Value;
        });

        // 4. Word-level abbreviations
        result = AbbrevRegex.Replace(result, match =>
        {
            var key = match.Groups[1].Value.ToLowerInvariant();
            return Abbreviations.TryGetValue(key, out var expansion) ? expansion : match.Value;
        });

        return result;
    }

    public static IReadOnlyList<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return SentenceSplitRegex.Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}
