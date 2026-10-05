using Maise.Core.ASR;
using Maise.Core.TTS;
using Xunit;

namespace Maise.Tests;

public class TokenizerTests
{
    [Fact]
    public void OpenPhonemizerInputTokenizer_EncodesProperly()
    {
        var input = "cat";
        var encoded = OpenPhonemizerInputTokenizer.Encode(input);

        // Start token: 1 (<en_us>), each char repeated 3 times, End token: 2 (<end>)
        // "c" -> 5, "a" -> 3, "t" -> 22
        // total length = 1 + 3*3 + 1 = 11
        Assert.Equal(11, encoded.Length);
        Assert.Equal(1, encoded[0]);
        Assert.Equal(5, encoded[1]);
        Assert.Equal(5, encoded[2]);
        Assert.Equal(5, encoded[3]);
        Assert.Equal(3, encoded[4]);
        Assert.Equal(3, encoded[5]);
        Assert.Equal(3, encoded[6]);
        Assert.Equal(22, encoded[7]);
        Assert.Equal(22, encoded[8]);
        Assert.Equal(22, encoded[9]);
        Assert.Equal(2, encoded[10]);
    }

    [Fact]
    public void OpenPhonemizerOutputTokenizer_EncodesPhonemes()
    {
        var encoded = OpenPhonemizerOutputTokenizer.Encode("kæt");
        // Wrapped with special id 0 at start and end
        Assert.True(encoded.Length >= 3);
        Assert.Equal(0, encoded[0]);
        Assert.Equal(0, encoded[^1]);
    }

    [Fact]
    public void WhisperTokenizer_BuildsUnicodeToByteCorrectly()
    {
        var map = WhisperTokenizer.BuildUnicodeToByte();
        Assert.Equal(256, map.Count);
        // ASCII printable character 'A' (65) maps to byte 65
        Assert.Equal((byte)'A', map['A']);
        Assert.Equal((byte)' ', map['\u0120']); // GPT-2 space token mapping
    }
}
