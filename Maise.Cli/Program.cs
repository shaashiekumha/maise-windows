using Maise.Core;
using Maise.Core.Audio;
using Maise.Core.TTS;

namespace Maise.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        try
        {
            using var engine = new MaiseEngine();

            switch (command)
            {
                case "speak" or "tts":
                    return HandleTts(engine, args.Skip(1).ToArray());

                case "listen" or "transcribe" or "asr":
                    return HandleAsr(engine, args.Skip(1).ToArray());

                case "voices":
                    HandleVoices();
                    return 0;

                case "server":
                    return await HandleServerAsync(engine, args.Skip(1).ToArray());

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
Maise CLI - On-device Neural Speech Engine for Windows
======================================================
Usage:
  maise tts <text> [--voice <voice_id>] [--speed <speed>] [--output <file.wav>] [--play]
  maise asr <audio_file.wav>
  maise voices
  maise server [--port 8033]

Examples:
  maise tts ""Hello world from local Kokoro neural TTS!"" --play
  maise tts ""Welcome to Maise for Windows"" --voice en-US-heart-kokoro --output speech.wav
  maise asr speech.wav
  maise server --port 8033
");
    }

    private static int HandleTts(MaiseEngine engine, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Please provide text to speak.");
            return 1;
        }

        var text = args[0];
        var voice = VoiceManager.DefaultVoiceId;
        var speed = 1.0f;
        string? outputFile = null;
        var play = false;

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] is "--voice" or "-v" && i + 1 < args.Length)
            {
                voice = args[++i];
            }
            else if (args[i] is "--speed" or "-s" && i + 1 < args.Length)
            {
                float.TryParse(args[++i], out speed);
            }
            else if (args[i] is "--output" or "-o" && i + 1 < args.Length)
            {
                outputFile = args[++i];
            }
            else if (args[i] is "--play" or "-p")
            {
                play = true;
            }
        }

        if (string.IsNullOrEmpty(outputFile) && !play)
        {
            play = true; // Default to play if no output file specified
        }

        Console.WriteLine($"[TTS] Synthesizing using voice '{voice}' (speed {speed}x)...");
        engine.InitializeTts();

        var startTime = DateTime.UtcNow;
        var samples = engine.Tts!.Synthesize(text, voice, speed);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var audioDuration = (double)samples.Length / KokoroTTS.SampleRate;

        Console.WriteLine($"[TTS] Synthesized {audioDuration:F2}s audio in {elapsed:F0}ms (RTF: {elapsed / 1000.0 / audioDuration:F2}x)");

        if (!string.IsNullOrEmpty(outputFile))
        {
            var fullPath = Path.GetFullPath(outputFile);
            WavUtility.SaveWavFile(fullPath, samples, KokoroTTS.SampleRate, 1);
            Console.WriteLine($"[TTS] Saved output WAV to: {fullPath}");
        }

        if (play)
        {
            Console.WriteLine("[TTS] Playing audio...");
            var tcs = new TaskCompletionSource();
            engine.Player.PlaybackFinished += () => tcs.TrySetResult();
            engine.Player.Play(samples, KokoroTTS.SampleRate, 1);
            tcs.Task.Wait();
        }

        return 0;
    }

    private static int HandleAsr(MaiseEngine engine, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Please provide path to a WAV audio file.");
            return 1;
        }

        var filePath = args[0];
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        Console.WriteLine($"[ASR] Loading '{filePath}'...");
        var wavBytes = File.ReadAllBytes(filePath);
        var (samples, sampleRate, _) = WavUtility.ReadWav(wavBytes);

        Console.WriteLine($"[ASR] Transcribing with Distil-Whisper ONNX...");
        engine.InitializeAsr();

        var startTime = DateTime.UtcNow;
        var transcript = engine.Asr!.Transcribe(samples, sampleRate);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        Console.WriteLine($"[ASR] Transcription completed in {elapsed:F0}ms:");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\"{transcript}\"");
        Console.ResetColor();

        return 0;
    }

    private static void HandleVoices()
    {
        Console.WriteLine($"Available Kokoro Voices ({VoiceManager.AllVoices.Count}):");
        Console.WriteLine("---------------------------------------------------------------");
        foreach (var v in VoiceManager.AllVoices)
        {
            Console.WriteLine($" • {v.Id.PadRight(28)} | {v.Name.PadRight(12)} | {v.Region.PadRight(16)} | {v.Gender}");
        }
    }

    private static async Task<int> HandleServerAsync(MaiseEngine engine, string[] args)
    {
        var port = 8033;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--port" or "-p" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out port);
            }
        }

        Console.WriteLine($"Starting Maise Local API Server on port {port}...");
        engine.StartServer(port);
        Console.WriteLine($"Server running at {engine.Server!.BaseUrl}");
        Console.WriteLine("Endpoints:");
        Console.WriteLine($" • POST {engine.Server.BaseUrl}v1/audio/speech");
        Console.WriteLine($" • POST {engine.Server.BaseUrl}v1/audio/transcriptions");
        Console.WriteLine($" • GET  {engine.Server.BaseUrl}v1/audio/voices");
        Console.WriteLine($" • GET  {engine.Server.BaseUrl}health");
        Console.WriteLine("\nPress Ctrl+C to stop.");

        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.TrySetResult();
        };

        await tcs.Task;
        Console.WriteLine("Stopping server...");
        engine.StopServer();
        return 0;
    }
}
