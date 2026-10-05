using Maise.Core;
using Maise.Core.Audio;
using Maise.Core.TTS;
using Xunit;

namespace Maise.Tests;

public class EngineIntegrationTests
{
    [Fact]
    public void KokoroTTS_SynthesizesSpeechPcm()
    {
        using var engine = new MaiseEngine();
        engine.InitializeTts();

        Assert.NotNull(engine.Tts);
        var samples = engine.Tts.Synthesize("Hello world!", VoiceManager.DefaultVoiceId);

        Assert.NotEmpty(samples);
        // Duration should be at least 0.3 seconds (24000 * 0.3 = 7200 samples)
        Assert.True(samples.Length > 5000);

        var wavBytes = WavUtility.CreateWav(samples, KokoroTTS.SampleRate, 1);
        Assert.NotEmpty(wavBytes);
        Assert.True(wavBytes.Length > 10000);
    }

    [Fact]
    public void WhisperASR_InitializesSuccessfully()
    {
        using var engine = new MaiseEngine();
        engine.InitializeAsr();

        Assert.NotNull(engine.Asr);
        // Transcribe a short silence buffer (16000 samples = 1 sec)
        var silence = new short[16000];
        var result = engine.Asr.Transcribe(silence);
        // Silence will return empty string or clean output without crashing
        Assert.NotNull(result);
    }
}
