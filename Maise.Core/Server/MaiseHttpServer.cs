using System.Net;
using System.Text;
using System.Text.Json;
using Maise.Core.Audio;
using Maise.Core.ASR;
using Maise.Core.TTS;

namespace Maise.Core.Server;

public class MaiseHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly KokoroTTS? _tts;
    private readonly WhisperASR? _asr;
    private readonly int _port;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public int Port => _port;
    public string BaseUrl => $"http://127.0.0.1:{_port}/";

    public MaiseHttpServer(int port = 8033, KokoroTTS? tts = null, WhisperASR? asr = null)
    {
        _port = port;
        _tts = tts;
        _asr = asr;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Prefixes.Add($"http://localhost:{_port}/");
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener.Start();
        IsRunning = true;
        _serverTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        Console.WriteLine($"[Maise.Server] Listening on {BaseUrl}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _cts?.Cancel();
        try
        {
            _listener.Stop();
        }
        catch { }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) when (!IsRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!IsRunning) break;
                Console.Error.WriteLine($"[Maise.Server] Listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // Apply CORS headers for local web apps / extensions
        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = (int)HttpStatusCode.NoContent;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";

            if (req.HttpMethod == "GET" && (path == "" || path == "/health"))
            {
                await WriteJsonAsync(res, new
                {
                    status = "ok",
                    service = "Maise Speech Engine (Windows)",
                    version = "1.0.0",
                    device = "CPU (AVX)",
                    endpoints = new[] { "/v1/audio/speech", "/v1/audio/transcriptions", "/v1/audio/voices", "/v1/models" }
                });
            }
            else if (req.HttpMethod == "GET" && path == "/v1/models")
            {
                await WriteJsonAsync(res, new
                {
                    data = new object[]
                    {
                        new { id = "kokoro", @object = "model", owned_by = "hexgrad" },
                        new { id = "distil-whisper", @object = "model", owned_by = "huggingface" }
                    }
                });
            }
            else if (req.HttpMethod == "GET" && path == "/v1/audio/voices")
            {
                await WriteJsonAsync(res, new
                {
                    default_voice = VoiceManager.DefaultVoiceId,
                    voices = VoiceManager.AllVoices
                });
            }
            else if (req.HttpMethod == "POST" && path == "/v1/audio/speech")
            {
                await HandleSpeechAsync(req, res);
            }
            else if (req.HttpMethod == "POST" && path == "/v1/audio/transcriptions")
            {
                await HandleTranscriptionAsync(req, res);
            }
            else
            {
                res.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteJsonAsync(res, new { error = "Not found" });
            }
        }
        catch (Exception ex)
        {
            res.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonAsync(res, new { error = ex.Message });
        }
        finally
        {
            try { res.Close(); } catch { }
        }
    }

    private async Task HandleSpeechAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        if (_tts == null)
        {
            res.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await WriteJsonAsync(res, new { error = "TTS engine is not initialized." });
            return;
        }

        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var input = root.TryGetProperty("input", out var inputProp) ? inputProp.GetString() ?? "" : "";
        var voice = root.TryGetProperty("voice", out var voiceProp) ? voiceProp.GetString() ?? VoiceManager.DefaultVoiceId : VoiceManager.DefaultVoiceId;
        var speed = root.TryGetProperty("speed", out var speedProp) ? (float)speedProp.GetDouble() : 1.0f;

        if (string.IsNullOrWhiteSpace(input))
        {
            res.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonAsync(res, new { error = "Field 'input' is required." });
            return;
        }

        var samples = _tts.Synthesize(input, voice, speed);
        var wavBytes = WavUtility.CreateWav(samples, KokoroTTS.SampleRate, 1);

        res.ContentType = "audio/wav";
        res.ContentLength64 = wavBytes.Length;
        await res.OutputStream.WriteAsync(wavBytes);
    }

    private async Task HandleTranscriptionAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        if (_asr == null)
        {
            res.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await WriteJsonAsync(res, new { error = "ASR engine is not initialized." });
            return;
        }

        using var ms = new MemoryStream();
        await req.InputStream.CopyToAsync(ms);
        var rawBytes = ms.ToArray();

        if (rawBytes.Length == 0)
        {
            res.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonAsync(res, new { error = "No audio payload provided." });
            return;
        }

        short[] samples;
        int sampleRate;

        // Check if WAV format or raw PCM
        if (rawBytes.Length > 12 && Encoding.ASCII.GetString(rawBytes, 0, 4) == "RIFF")
        {
            var wavInfo = WavUtility.ReadWav(rawBytes);
            samples = wavInfo.samples;
            sampleRate = wavInfo.sampleRate;
        }
        else
        {
            // Assume 16kHz 16-bit PCM
            var count = rawBytes.Length / 2;
            samples = new short[count];
            for (var i = 0; i < count; i++)
            {
                samples[i] = BitConverter.ToInt16(rawBytes, i * 2);
            }
            sampleRate = WhisperASR.SampleRate;
        }

        var transcript = _asr.Transcribe(samples, sampleRate);
        await WriteJsonAsync(res, new { text = transcript });
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, object data)
    {
        res.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
