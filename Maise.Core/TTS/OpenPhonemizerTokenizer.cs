namespace Maise.Core.TTS;

public static class OpenPhonemizerInputTokenizer
{
    public const int BlankId = 0;
    public const int EndId = 2;
    public const int CharRepeats = 3;

    public static readonly IReadOnlyDictionary<string, int> TextSymbols = new Dictionary<string, int>
    {
        ["_"] = 0,
        ["<en_us>"] = 1,
        ["<end>"] = 2,
        ["a"] = 3, ["b"] = 4, ["c"] = 5, ["d"] = 6, ["e"] = 7,
        ["f"] = 8, ["g"] = 9, ["h"] = 10, ["i"] = 11, ["j"] = 12,
        ["k"] = 13, ["l"] = 14, ["m"] = 15, ["n"] = 16, ["o"] = 17,
        ["p"] = 18, ["q"] = 19, ["r"] = 20, ["s"] = 21, ["t"] = 22,
        ["u"] = 23, ["v"] = 24, ["w"] = 25, ["x"] = 26, ["y"] = 27,
        ["z"] = 28,
        ["A"] = 29, ["B"] = 30, ["C"] = 31, ["D"] = 32, ["E"] = 33,
        ["F"] = 34, ["G"] = 35, ["H"] = 36, ["I"] = 37, ["J"] = 38,
        ["K"] = 39, ["L"] = 40, ["M"] = 41, ["N"] = 42, ["O"] = 43,
        ["P"] = 44, ["Q"] = 45, ["R"] = 46, ["S"] = 47, ["T"] = 48,
        ["U"] = 49, ["V"] = 50, ["W"] = 51, ["X"] = 52, ["Y"] = 53,
        ["Z"] = 54,
    };

    public static readonly IReadOnlyDictionary<int, string> PhonemeSymbols = new Dictionary<int, string>
    {
        [0] = "_",
        [1] = "<en_us>",
        [2] = "<end>",
        [3] = "a", [4] = "b", [5] = "d", [6] = "e", [7] = "f",
        [8] = "g", [9] = "h", [10] = "i", [11] = "j", [12] = "k",
        [13] = "l", [14] = "m", [15] = "n", [16] = "o", [17] = "p",
        [18] = "r", [19] = "s", [20] = "t", [21] = "u", [22] = "v",
        [23] = "w", [24] = "x", [25] = "y", [26] = "z", [27] = "æ",
        [28] = "ç", [29] = "ð", [30] = "ø", [31] = "ŋ", [32] = "œ",
        [33] = "ɐ", [34] = "ɑ", [35] = "ɔ", [36] = "ə", [37] = "ɛ",
        [38] = "ɜ", [39] = "ɝ", [40] = "ɹ", [41] = "ɚ", [42] = "ɡ",
        [43] = "ɪ", [44] = "ʁ", [45] = "ʃ", [46] = "ʊ", [47] = "ʌ",
        [48] = "ʏ", [49] = "ʒ", [50] = "ʔ", [51] = "ˈ", [52] = "ˌ",
        [53] = "ː", [54] = "̃", [55] = "̍", [56] = "̥", [57] = "̩",
        [58] = "̯", [59] = "͡", [60] = "θ", [61] = "'", [62] = "ɾ",
        [63] = "ᵻ"
    };

    public static int[] Encode(string text)
    {
        var cleaned = text.ToLowerInvariant().Replace(' ', '_').Trim();
        var outList = new List<int>(cleaned.Length * CharRepeats + 2);

        var langToken = TextSymbols["<en_us>"];
        outList.Add(langToken);

        foreach (var ch in cleaned)
        {
            if (TextSymbols.TryGetValue(ch.ToString(), out var code))
            {
                for (var r = 0; r < CharRepeats; r++)
                {
                    outList.Add(code);
                }
            }
        }

        var endToken = TextSymbols["<end>"];
        outList.Add(endToken);

        return outList.ToArray();
    }
}

public static class OpenPhonemizerOutputTokenizer
{
    private const int SpecialId = 0; // "$" pad / unk / wrapper

    private static readonly IReadOnlyDictionary<string, int> Vocab = new Dictionary<string, int>
    {
        ["$"] = 0,
        [";"] = 1,
        [":"] = 2,
        [","] = 3,
        ["."] = 4,
        ["!"] = 5,
        ["?"] = 6,
        ["\u2014"] = 9,   // —
        ["\u2026"] = 10,  // …
        ["\""] = 11,
        ["("] = 12,
        [")"] = 13,
        ["\u201C"] = 14,  // “
        ["\u201D"] = 15,  // ”
        [" "] = 16,
        ["\u0303"] = 17,  // ̃
        ["\u02A3"] = 18,  // ʣ
        ["\u02A5"] = 19,  // ʥ
        ["\u02A6"] = 20,  // ʦ
        ["\u02A8"] = 21,  // ʨ
        ["\u1D5D"] = 22,  // ᵝ
        ["\uAB67"] = 23,  // ꭧ
        ["A"] = 24,
        ["I"] = 25,
        ["O"] = 31,
        ["Q"] = 33,
        ["S"] = 35,
        ["T"] = 36,
        ["W"] = 39,
        ["Y"] = 41,
        ["\u1D4A"] = 42,  // ᵊ
        ["a"] = 43, ["b"] = 44, ["c"] = 45, ["d"] = 46, ["e"] = 47,
        ["f"] = 48, ["g"] = 49, ["h"] = 50, ["i"] = 51, ["j"] = 52,
        ["k"] = 53, ["l"] = 54, ["m"] = 55, ["n"] = 56, ["o"] = 57,
        ["p"] = 58, ["q"] = 59, ["r"] = 60, ["s"] = 61, ["t"] = 62,
        ["u"] = 63, ["v"] = 64, ["w"] = 65, ["x"] = 66, ["y"] = 67,
        ["z"] = 68,
        ["\u0251"] = 69,  // ɑ
        ["\u0250"] = 70,  // ɐ
        ["\u0252"] = 71,  // ɒ
        ["\u00E6"] = 72,  // æ
        ["\u03B2"] = 75,  // β
        ["\u0254"] = 76,  // ɔ
        ["\u0255"] = 77,  // ɕ
        ["\u00E7"] = 78,  // ç
        ["\u0256"] = 80,  // ɖ
        ["\u00F0"] = 81,  // ð
        ["\u02A4"] = 82,  // ʤ
        ["\u0259"] = 83,  // ə
        ["\u025A"] = 85,  // ɚ
        ["\u025B"] = 86,  // ɛ
        ["\u025C"] = 87,  // ɜ
        ["\u025F"] = 90,  // ɟ
        ["\u0261"] = 92,  // ɡ
        ["\u0265"] = 99,  // ɥ
        ["\u0268"] = 101, // ɨ
        ["\u026A"] = 102, // ɪ
        ["\u029D"] = 103, // ʝ
        ["\u0270"] = 111, // ɰ
        ["\u014B"] = 112, // ŋ
        ["\u0273"] = 113, // ɳ
        ["\u0272"] = 114, // ɲ
        ["\u0274"] = 115, // ɴ
        ["\u00F8"] = 116, // ø
        ["\u0278"] = 118, // ɸ
        ["\u03B8"] = 119, // θ
        ["\u0153"] = 120, // œ
        ["\u0279"] = 123, // ɹ
        ["\u027E"] = 125, // ɾ
        ["\u027B"] = 126, // ɻ
        ["\u0281"] = 128, // ʁ
        ["\u027D"] = 129, // ɽ
        ["\u0282"] = 130, // ʂ
        ["\u0283"] = 131, // ʃ
        ["\u0288"] = 132, // ʈ
        ["\u02A7"] = 133, // ʧ
        ["\u028A"] = 135, // ʊ
        ["\u028B"] = 136, // ʋ
        ["\u028C"] = 138, // ʌ
        ["\u0263"] = 139, // ɣ
        ["\u0264"] = 140, // ɤ
        ["\u03C7"] = 142, // χ
        ["\u028E"] = 143, // ʎ
        ["\u0292"] = 147, // ʒ
        ["\u02C8"] = 156, // ˈ
        ["\u02CC"] = 157, // ˌ
        ["\u02D0"] = 158, // ː
        ["\u02B0"] = 162, // ʰ
        ["\u02B2"] = 164, // ʲ
        ["\u2193"] = 169, // ↓
        ["\u2192"] = 171, // →
        ["\u2197"] = 172, // ↗
        ["\u2198"] = 173, // ↘
        ["\u1D3B"] = 177, // ᵻ
    };

    public static int[] Encode(string phonemes)
    {
        var ids = new List<int>(phonemes.Length + 2) { SpecialId };
        foreach (var ch in phonemes)
        {
            ids.Add(Vocab.TryGetValue(ch.ToString(), out var id) ? id : SpecialId);
        }
        ids.Add(SpecialId);
        return ids.ToArray();
    }
}
