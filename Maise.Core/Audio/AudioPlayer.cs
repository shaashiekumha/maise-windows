using NAudio.Wave;

namespace Maise.Core.Audio;

public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private bool _disposed;

    public event Action? PlaybackFinished;

    public void Play(short[] samples, int sampleRate = 24000, short channels = 1)
    {
        Stop();

        var waveFormat = new WaveFormat(sampleRate, 16, channels);
        _bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferLength = samples.Length * 2 + 8192,
            DiscardOnBufferOverflow = true
        };

        var byteBuffer = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, byteBuffer, 0, byteBuffer.Length);
        _bufferedWaveProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_bufferedWaveProvider);
        _waveOut.PlaybackStopped += (s, e) => PlaybackFinished?.Invoke();
        _waveOut.Play();
    }

    public void Stop()
    {
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _bufferedWaveProvider = null;
        }
        catch
        {
            // Ignore stop errors
        }
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
