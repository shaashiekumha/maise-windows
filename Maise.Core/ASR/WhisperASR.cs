using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Maise.Core.ASR;

public class WhisperASR : IDisposable
{
    public const int SampleRate = 16000;
    private const int NFft = 400;
    private const int HopLength = 160;
    private const int NMels = 80;
    private const int ChunkSeconds = 30;
    private const int NSamples = SampleRate * ChunkSeconds; // 480,000
    private const int NFrames = NSamples / HopLength;       // 3,000
    private const double FMin = 0.0;
    private const double FMax = 8000.0;

    private const int FftSize = 512;
    private const int NFreqBins = FftSize / 2 + 1; // 257
    private const int MaxNewTokens = 448;

    private readonly InferenceSession _encoderSession;
    private readonly InferenceSession _decoderSession;
    private readonly WhisperTokenizer _tokenizer;

    private readonly float[] _hannWindow;
    private readonly float[][] _melFilterbank;
    private bool _disposed;

    public WhisperASR(string encoderPath, string decoderPath, string vocabPath)
    {
        if (!File.Exists(encoderPath))
            throw new FileNotFoundException($"Whisper encoder model not found: {encoderPath}");
        if (!File.Exists(decoderPath))
            throw new FileNotFoundException($"Whisper decoder model not found: {decoderPath}");

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = Environment.ProcessorCount
        };

        _encoderSession = new InferenceSession(encoderPath, sessionOptions);
        _decoderSession = new InferenceSession(decoderPath, sessionOptions);
        _tokenizer = new WhisperTokenizer(vocabPath);

        _hannWindow = new float[NFft];
        for (var i = 0; i < NFft; i++)
        {
            _hannWindow[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (NFft - 1))));
        }

        _melFilterbank = BuildMelFilterbank();
    }

    public string Transcribe(short[] audioSamples, int inputSampleRate = SampleRate, Action<string>? onPartial = null)
    {
        var floatAudio = ResampleToFloat(audioSamples, inputSampleRate);
        var mel = ComputeLogMelSpectrogram(floatAudio);
        return RunInference(mel, onPartial);
    }

    private static float[] ResampleToFloat(short[] samples, int srcRate)
    {
        var floatSrc = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            floatSrc[i] = samples[i] / 32768.0f;
        }

        if (srcRate == SampleRate) return floatSrc;

        var ratio = (double)srcRate / SampleRate;
        var outLen = (int)(floatSrc.Length / ratio);
        var output = new float[outLen];

        for (var i = 0; i < outLen; i++)
        {
            var src = i * ratio;
            var lo = Math.Clamp((int)src, 0, floatSrc.Length - 1);
            var hi = Math.Clamp(lo + 1, 0, floatSrc.Length - 1);
            var frac = (float)(src - lo);
            output[i] = floatSrc[lo] * (1.0f - frac) + floatSrc[hi] * frac;
        }
        return output;
    }

    private float[] ComputeLogMelSpectrogram(float[] audio)
    {
        var mel = new float[NMels, NFrames];
        var silenceLog = (float)Math.Log10(1e-10);
        var audioFrames = Math.Min((audio.Length + HopLength - 1) / HopLength + 1, NFrames);

        var re = new float[FftSize];
        var im = new float[FftSize];

        for (var frame = 0; frame < NFrames; frame++)
        {
            if (frame >= audioFrames)
            {
                for (var m = 0; m < NMels; m++) mel[m, frame] = silenceLog;
                continue;
            }

            var start = frame * HopLength;
            Array.Clear(re, 0, FftSize);
            Array.Clear(im, 0, FftSize);

            for (var j = 0; j < NFft; j++)
            {
                var s = start + j;
                re[j] = (s < audio.Length ? audio[s] : 0.0f) * _hannWindow[j];
            }

            FftInPlace(re, im);

            for (var m = 0; m < NMels; m++)
            {
                var sum = 0.0f;
                var row = _melFilterbank[m];
                for (var k = 0; k < NFreqBins; k++)
                {
                    sum += (re[k] * re[k] + im[k] * im[k]) * row[k];
                }
                mel[m, frame] = sum;
            }
        }

        var maxLog = float.NegativeInfinity;
        for (var m = 0; m < NMels; m++)
        {
            for (var f = 0; f < NFrames; f++)
            {
                var v = f < audioFrames ? (float)Math.Log10(Math.Max(mel[m, f], 1e-10f)) : silenceLog;
                mel[m, f] = v;
                if (v > maxLog) maxLog = v;
            }
        }

        var result = new float[NMels * NFrames];
        var floor = maxLog - 8.0f;
        for (var m = 0; m < NMels; m++)
        {
            var b = m * NFrames;
            for (var f = 0; f < NFrames; f++)
            {
                result[b + f] = (Math.Max(mel[m, f], floor) + 4.0f) / 4.0f;
            }
        }
        return result;
    }

    private static void FftInPlace(float[] re, float[] im)
    {
        var n = re.Length;
        var j = 0;
        for (var i = 1; i < n; i++)
        {
            var bit = n >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var half = len >> 1;
            var ang = -Math.PI / half;
            var wRe = (float)Math.Cos(ang);
            var wIm = (float)Math.Sin(ang);

            for (var i = 0; i < n; i += len)
            {
                var curRe = 1.0f;
                var curIm = 0.0f;

                for (var k = 0; k < half; k++)
                {
                    var uRe = re[i + k];
                    var uIm = im[i + k];
                    var vRe = re[i + k + half] * curRe - im[i + k + half] * curIm;
                    var vIm = re[i + k + half] * curIm + im[i + k + half] * curRe;

                    re[i + k] = uRe + vRe;
                    im[i + k] = uIm + vIm;
                    re[i + k + half] = uRe - vRe;
                    im[i + k + half] = uIm - vIm;

                    var newRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = newRe;
                }
            }
        }
    }

    private static float[][] BuildMelFilterbank()
    {
        var filterbank = new float[NMels][];
        for (var i = 0; i < NMels; i++) filterbank[i] = new float[NFreqBins];

        var fftFreqs = new double[NFreqBins];
        for (var k = 0; k < NFreqBins; k++)
        {
            fftFreqs[k] = (double)k * SampleRate / FftSize;
        }

        var melMin = HzToMelSlaney(FMin);
        var melMax = HzToMelSlaney(FMax);
        var hzPts = new double[NMels + 2];
        for (var i = 0; i < NMels + 2; i++)
        {
            hzPts[i] = MelToHzSlaney(melMin + i * (melMax - melMin) / (NMels + 1));
        }

        for (var m = 0; m < NMels; m++)
        {
            var fLo = hzPts[m];
            var fCtr = hzPts[m + 1];
            var fHi = hzPts[m + 2];
            var enorm = (float)(2.0 / (fHi - fLo));

            for (var k = 0; k < NFreqBins; k++)
            {
                var f = fftFreqs[k];
                var w = f < fLo || f > fHi ? 0.0 :
                        f < fCtr ? (f - fLo) / (fCtr - fLo) :
                                   (fHi - f) / (fHi - fCtr);
                filterbank[m][k] = (float)(w * enorm);
            }
        }
        return filterbank;
    }

    private static double HzToMelSlaney(double hz)
    {
        const double fSp = 200.0 / 3.0;
        const double minLogHz = 1000.0;
        const double minLogMel = minLogHz / fSp;
        var logStep = Math.Log(6.4) / 27.0;
        return hz < minLogHz ? hz / fSp : minLogMel + Math.Log(hz / minLogHz) / logStep;
    }

    private static double MelToHzSlaney(double mel)
    {
        const double fSp = 200.0 / 3.0;
        const double minLogHz = 1000.0;
        var logStep = Math.Log(6.4) / 27.0;
        return mel < minLogHz / fSp ? mel * fSp : minLogHz * Math.Exp((mel - minLogHz / fSp) * logStep);
    }

    private string RunInference(float[] melData, Action<string>? onPartial)
    {
        var melTensor = new DenseTensor<float>(melData, new[] { 1, NMels, NFrames });
        var encoderInputs = new[] { NamedOnnxValue.CreateFromTensor("input_features", melTensor) };

        using var encoderResult = _encoderSession.Run(encoderInputs);
        var encoderHiddenTensor = encoderResult.First().Value as DenseTensor<float>
            ?? encoderResult.First().AsTensor<float>();

        var allTokens = new List<int>
        {
            WhisperTokenizer.TokenSot,
            WhisperTokenizer.TokenEn,
            WhisperTokenizer.TokenTranscribe,
            WhisperTokenizer.TokenNoTimestamps
        };
        var textTokens = new List<int>();

        for (var i = 0; i < MaxNewTokens; i++)
        {
            var ids = allTokens.Select(t => (long)t).ToArray();
            var idsTensor = new DenseTensor<long>(ids, new[] { 1, ids.Length });

            var decoderInputs = new[]
            {
                NamedOnnxValue.CreateFromTensor("input_ids", idsTensor),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderHiddenTensor)
            };

            using var decoderResult = _decoderSession.Run(decoderInputs);
            var logitsTensor = decoderResult.First().AsTensor<float>();
            var nextToken = ArgmaxLastPosition(logitsTensor);

            allTokens.Add(nextToken);
            if (nextToken == WhisperTokenizer.TokenEot) break;

            if (nextToken < WhisperTokenizer.TokenEot)
            {
                textTokens.Add(nextToken);
                if (onPartial != null && _tokenizer.IsWordStart(nextToken))
                {
                    var partial = _tokenizer.Decode(textTokens).Trim();
                    if (!string.IsNullOrEmpty(partial))
                    {
                        onPartial(partial);
                    }
                }
            }
        }

        return _tokenizer.Decode(textTokens).Trim();
    }

    private static int ArgmaxLastPosition(Tensor<float> logits)
    {
        // Shape [1, seq_len, vocab_size]
        var seqLen = logits.Dimensions[1];
        var vocabSize = logits.Dimensions[2];
        var lastSeqIdx = seqLen - 1;

        var bestIdx = 0;
        var maxVal = logits[0, lastSeqIdx, 0];

        for (var v = 1; v < vocabSize; v++)
        {
            var val = logits[0, lastSeqIdx, v];
            if (val > maxVal)
            {
                maxVal = val;
                bestIdx = v;
            }
        }
        return bestIdx;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _encoderSession.Dispose();
            _decoderSession.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
