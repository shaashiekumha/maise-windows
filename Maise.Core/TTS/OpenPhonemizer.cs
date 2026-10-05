using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Maise.Core.Text;

namespace Maise.Core.TTS;

public class OpenPhonemizer : IDisposable
{
    private static readonly HashSet<string> PunctuationBefore = new() { ".", ",", "!", "?", ";", ":", ")", "]", "}", "\u00BB", "\u201D" };
    private static readonly HashSet<string> PunctuationAfter = new() { "(", "[", "{", "\u00AB", "\u201C" };
    private static readonly Regex WordRegex = new(@"[\w']+|[^\w\s]", RegexOptions.Compiled);
    private static readonly Regex PunctOnlyRegex = new(@"^[^\w']+$", RegexOptions.Compiled);

    private readonly InferenceSession _session;
    private readonly Dictionary<string, string> _dictionary;
    private bool _disposed;

    public OpenPhonemizer(string modelPath, string dictionaryPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Phonemizer model not found: {modelPath}");
        if (!File.Exists(dictionaryPath))
            throw new FileNotFoundException($"Phonemizer dictionary not found: {dictionaryPath}");

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        _session = new InferenceSession(modelPath, sessionOptions);
        _dictionary = LoadDictionary(dictionaryPath);
    }

    private static Dictionary<string, string> LoadDictionary(string dictionaryPath)
    {
        var map = new Dictionary<string, string>(130_000, StringComparer.OrdinalIgnoreCase);
        try
        {
            using var fs = File.OpenRead(dictionaryPath);
            using var doc = JsonDocument.Parse(fs);

            if (doc.RootElement.TryGetProperty("en_us", out var enUsProp) && enUsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in enUsProp.EnumerateObject())
                {
                    map[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[OpenPhonemizer] Error reading dictionary: {ex.Message}");
        }
        return map;
    }

    public string Phonemize(string text)
    {
        var normalized = TextNormalizer.NormalizeText(text);
        var matches = WordRegex.Matches(normalized);
        var tokens = matches.Select(m => m.Value).ToList();
        var sb = new StringBuilder();

        foreach (var token in tokens)
        {
            var isPunct = PunctOnlyRegex.IsMatch(token);

            if (isPunct)
            {
                if (PunctuationBefore.Contains(token))
                {
                    if (sb.Length > 0 && sb[^1] == ' ')
                    {
                        sb.Length--;
                    }
                    sb.Append(token);
                }
                else if (PunctuationAfter.Contains(token))
                {
                    sb.Append(token);
                }
                else
                {
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                    sb.Append(token);
                }
            }
            else
            {
                if (sb.Length > 0 && sb[^1] != ' ' && !PunctuationAfter.Contains(sb[^1].ToString()))
                {
                    sb.Append(' ');
                }
                var word = token.ToLowerInvariant();
                if (_dictionary.TryGetValue(word, out var phonemes))
                {
                    sb.Append(phonemes);
                }
                else
                {
                    sb.Append(PhonemizeWord(word));
                }
            }
        }

        return sb.ToString().Trim();
    }

    private string PhonemizeWord(string word)
    {
        var encoded = OpenPhonemizerInputTokenizer.Encode(word);
        var padded = new long[64];
        for (var i = 0; i < 64; i++)
        {
            padded[i] = i < encoded.Length ? encoded[i] : 0L;
        }

        var inputTensor = new DenseTensor<long>(padded, new[] { 1, 64 });
        var inputs = new[] { NamedOnnxValue.CreateFromTensor("text", inputTensor) };

        using var results = _session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        // Output shape is [1, 64, 64] -> batch=1, seq_len=64, vocab=64
        var sb = new StringBuilder();
        var prev = -1;

        for (var pos = 0; pos < 64; pos++)
        {
            var bestIdx = 0;
            var maxVal = outputTensor[0, pos, 0];

            for (var v = 1; v < 64; v++)
            {
                var val = outputTensor[0, pos, v];
                if (val > maxVal)
                {
                    maxVal = val;
                    bestIdx = v;
                }
            }

            // CTC decode
            if (bestIdx == prev) continue;
            prev = bestIdx;

            if (bestIdx == OpenPhonemizerInputTokenizer.BlankId) continue;
            if (bestIdx == OpenPhonemizerInputTokenizer.EndId) break;

            if (OpenPhonemizerInputTokenizer.PhonemeSymbols.TryGetValue(bestIdx, out var sym))
            {
                if (sym.StartsWith('<')) continue;
                sb.Append(sym);
            }
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
