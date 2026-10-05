using Maise.Core.ASR;
using Maise.Core.Audio;
using Maise.Core.Server;
using Maise.Core.TTS;

namespace Maise.Core;

public class MaiseEngine : IDisposable
{
    public string AssetsDirectory { get; }
    public KokoroTTS? Tts { get; private set; }
    public WhisperASR? Asr { get; private set; }
    public AudioPlayer Player { get; }
    public AudioRecorder Recorder { get; }
    public MaiseHttpServer? Server { get; private set; }

    private bool _disposed;

    public MaiseEngine(string? customAssetsPath = null)
    {
        AssetsDirectory = FindAssetsDirectory(customAssetsPath);
        Player = new AudioPlayer();
        Recorder = new AudioRecorder();
    }

    public static string FindAssetsDirectory(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
            return Path.GetFullPath(customPath);

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "assets"),
            Path.Combine(Directory.GetCurrentDirectory(), "assets"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets"),
            @"C:\Working-Now\maise-windows\assets"
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath) &&
                File.Exists(Path.Combine(fullPath, "tts", "kokoro-quantized.onnx")))
            {
                return fullPath;
            }
        }

        throw new DirectoryNotFoundException("Could not find the 'assets' directory containing Maise ONNX models.");
    }

    public void InitializeTts()
    {
        if (Tts != null) return;

        var kokoroPath = Path.Combine(AssetsDirectory, "tts", "kokoro-quantized.onnx");
        var phonemizerPath = Path.Combine(AssetsDirectory, "tts", "open-phonemizer.onnx");
        var dictPath = Path.Combine(AssetsDirectory, "tts", "dictionary.json");
        var voicesDir = Path.Combine(AssetsDirectory, "tts", "voices");

        Tts = new KokoroTTS(kokoroPath, phonemizerPath, dictPath, voicesDir);
    }

    public void InitializeAsr()
    {
        if (Asr != null) return;

        var encoderPath = Path.Combine(AssetsDirectory, "asr", "encoder_model_quantized.onnx");
        var decoderPath = Path.Combine(AssetsDirectory, "asr", "decoder_model_quantized.onnx");
        var vocabPath = Path.Combine(AssetsDirectory, "asr", "vocab.json");

        Asr = new WhisperASR(encoderPath, decoderPath, vocabPath);
    }

    public void StartServer(int port = 8033)
    {
        if (Server?.IsRunning == true) return;

        InitializeTts();
        InitializeAsr();

        Server = new MaiseHttpServer(port, Tts, Asr);
        Server.Start();
    }

    public void StopServer()
    {
        Server?.Stop();
        Server?.Dispose();
        Server = null;
    }

    public void Speak(string text, string voiceId = VoiceManager.DefaultVoiceId, float speed = 1.0f)
    {
        InitializeTts();
        var pcm = Tts!.Synthesize(text, voiceId, speed);
        Player.Play(pcm, KokoroTTS.SampleRate, 1);
    }

    public void StopSpeaking()
    {
        Player.Stop();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopServer();
            Player.Dispose();
            Recorder.Dispose();
            Tts?.Dispose();
            Asr?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
