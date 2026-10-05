using System.Buffers.Binary;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Maise.Core.Text;

namespace Maise.Core.TTS;

public class KokoroTTS : IDisposable
{
    public const int SampleRate = 24000;
    private const int StyleDim = 256;
    private const int MaxPhonemeLength = 510;

    private readonly OpenPhonemizer _phonemizer;
    private readonly InferenceSession _session;
    private readonly string _voicesDirectory;
    private bool _disposed;

    public KokoroTTS(string kokoroModelPath, string phonemizerModelPath, string dictionaryPath, string voicesDirectory)
    {
        if (!File.Exists(kokoroModelPath))
            throw new FileNotFoundException($"Kokoro model not found: {kokoroModelPath}");
        if (!Directory.Exists(voicesDirectory))
            throw new DirectoryNotFoundException($"Voices directory not found: {voicesDirectory}");

        _phonemizer = new OpenPhonemizer(phonemizerModelPath, dictionaryPath);
        _voicesDirectory = voicesDirectory;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        _session = new InferenceSession(kokoroModelPath, sessionOptions);
    }

    public short[] Synthesize(string text, string voiceId = VoiceManager.DefaultVoiceId, float speed = 1.0f)
    {
        var sentences = TextNormalizer.SplitSentences(text);
        if (sentences.Count == 0)
        {
            sentences = new[] { text };
        }

        var allSamples = new List<short>();
        foreach (var sentence in sentences)
        {
            var chunk = SynthesizeSentence(sentence, voiceId, speed);
            if (chunk.Length > 0)
            {
                allSamples.AddRange(chunk);
            }
        }
        return allSamples.ToArray();
    }

    public async IAsyncEnumerable<short[]> SynthesizeStreamAsync(string text, string voiceId = VoiceManager.DefaultVoiceId, float speed = 1.0f)
    {
        var sentences = TextNormalizer.SplitSentences(text);
        if (sentences.Count == 0)
        {
            sentences = new[] { text };
        }

        foreach (var sentence in sentences)
        {
            var chunk = await Task.Run(() => SynthesizeSentence(sentence, voiceId, speed));
            if (chunk.Length > 0)
            {
                yield return chunk;
            }
        }
    }

    private short[] SynthesizeSentence(string text, string voiceId, float speed)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<short>();

        // 1. Phonemize
        var phonemes = _phonemizer.Phonemize(text);
        if (string.IsNullOrWhiteSpace(phonemes)) return Array.Empty<short>();

        // 2. Tokenize phonemes (adds wrapper token 0 at both ends)
        var tokens = OpenPhonemizerOutputTokenizer.Encode(phonemes);
        if (tokens.Length <= 2) return Array.Empty<short>();

        // 3. n_tokens is phoneme count without the two wrapping special tokens, capped at 509
        var nTokens = Math.Min(Math.Max(tokens.Length - 2, 0), MaxPhonemeLength - 1);

        // 4. Load voice style embedding
        var styleData = LoadVoiceStyle(voiceId, nTokens);

        // 5. Build ONNX inputs
        var inputIds = new long[tokens.Length];
        for (var i = 0; i < tokens.Length; i++) inputIds[i] = tokens[i];

        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, tokens.Length });
        var styleTensor = new DenseTensor<float>(styleData, new[] { 1, StyleDim });
        var speedTensor = new DenseTensor<float>(new[] { speed }, new[] { 1 });

        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("style", styleTensor),
            NamedOnnxValue.CreateFromTensor("speed", speedTensor),
        };

        using var results = _session.Run(inputs);
        var waveformTensor = results.First().AsTensor<float>();

        // 6. Convert float32 [-1, 1] to 16-bit PCM samples
        var length = waveformTensor.Length;
        var pcm = new short[length];
        var index = 0;

        foreach (var sample in waveformTensor)
        {
            var clamped = Math.Clamp(sample, -1.0f, 1.0f);
            pcm[index++] = (short)(clamped * 32767.0f);
        }

        return pcm;
    }

    private float[] LoadVoiceStyle(string voiceId, int nTokens)
    {
        // Normalize voiceId
        var cleanVoiceId = voiceId.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(voiceId)
            : voiceId;

        var binPath = Path.Combine(_voicesDirectory, $"{cleanVoiceId}.bin");
        if (!File.Exists(binPath))
        {
            // Try searching by partial match or fallback to default
            var match = Directory.GetFiles(_voicesDirectory, $"*{cleanVoiceId}*.bin").FirstOrDefault();
            binPath = match ?? Path.Combine(_voicesDirectory, $"{VoiceManager.DefaultVoiceId}.bin");
        }

        var bytes = File.ReadAllBytes(binPath);
        var floatCount = bytes.Length / 4;
        var allFloats = new float[floatCount];

        for (var i = 0; i < floatCount; i++)
        {
            allFloats[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4, 4));
        }

        var offset = nTokens * StyleDim;
        var result = new float[StyleDim];

        if (offset + StyleDim <= floatCount)
        {
            Array.Copy(allFloats, offset, result, 0, StyleDim);
        }
        else
        {
            var safeOffset = Math.Max(0, floatCount - StyleDim);
            Array.Copy(allFloats, safeOffset, result, 0, StyleDim);
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _phonemizer.Dispose();
            _session.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
