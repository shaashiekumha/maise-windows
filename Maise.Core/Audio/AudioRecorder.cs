using NAudio.Wave;

namespace Maise.Core.Audio;

public class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly MemoryStream _recordStream = new();
    private bool _isRecording;
    private bool _disposed;

    public event Action<float>? AudioLevelChanged;

    public bool IsRecording => _isRecording;

    public void StartRecording(int sampleRate = 16000)
    {
        if (_isRecording) return;

        _recordStream.SetLength(0);
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(sampleRate, 16, 1),
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        _isRecording = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isRecording) return;
        _recordStream.Write(e.Buffer, 0, e.BytesRecorded);

        // Compute RMS audio level for waveform visualizer
        var sampleCount = e.BytesRecorded / 2;
        var sumSquares = 0.0;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i * 2) / 32768.0;
            sumSquares += sample * sample;
        }
        var rms = (float)Math.Sqrt(sumSquares / Math.Max(1, sampleCount));
        AudioLevelChanged?.Invoke(Math.Clamp(rms * 4.0f, 0.0f, 1.0f));
    }

    public short[] StopRecording()
    {
        if (!_isRecording) return Array.Empty<short>();

        _isRecording = false;
        try
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
        }
        catch
        {
            // Ignore stop errors
        }

        var bytes = _recordStream.ToArray();
        var sampleCount = bytes.Length / 2;
        var samples = new short[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(bytes, i * 2);
        }
        return samples;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopRecording();
            _recordStream.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
