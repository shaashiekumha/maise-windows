using System.Text;

namespace Maise.Core.Audio;

public static class WavUtility
{
    public static byte[] CreateWav(short[] samples, int sampleRate = 24000, short channels = 1)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        var byteRate = sampleRate * channels * 2;
        var blockAlign = (short)(channels * 2);
        var subChunk2Size = samples.Length * 2;
        var chunkSize = 36 + subChunk2Size;

        // RIFF Header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt Subchunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);          // Subchunk1Size (16 for PCM)
        bw.Write((short)1);    // AudioFormat (1 for PCM)
        bw.Write(channels);    // NumChannels
        bw.Write(sampleRate);  // SampleRate
        bw.Write(byteRate);    // ByteRate
        bw.Write(blockAlign);  // BlockAlign
        bw.Write((short)16);   // BitsPerSample

        // data Subchunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(subChunk2Size);

        foreach (var sample in samples)
        {
            bw.Write(sample);
        }

        bw.Flush();
        return ms.ToArray();
    }

    public static void SaveWavFile(string filePath, short[] samples, int sampleRate = 24000, short channels = 1)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var bytes = CreateWav(samples, sampleRate, channels);
        File.WriteAllBytes(filePath, bytes);
    }

    public static (short[] samples, int sampleRate, int channels) ReadWav(byte[] wavBytes)
    {
        using var ms = new MemoryStream(wavBytes);
        using var br = new BinaryReader(ms);

        var riff = Encoding.ASCII.GetString(br.ReadBytes(4));
        if (riff != "RIFF") throw new InvalidDataException("Not a valid RIFF WAV file.");

        br.ReadInt32(); // ChunkSize
        var wave = Encoding.ASCII.GetString(br.ReadBytes(4));
        if (wave != "WAVE") throw new InvalidDataException("Not a valid WAVE format.");

        short channels = 1;
        var sampleRate = 16000;
        short bitsPerSample = 16;
        short[]? samples = null;

        while (ms.Position < ms.Length)
        {
            var chunkId = Encoding.ASCII.GetString(br.ReadBytes(4));
            var chunkSize = br.ReadInt32();

            if (chunkId == "fmt ")
            {
                var audioFormat = br.ReadInt16();
                channels = br.ReadInt16();
                sampleRate = br.ReadInt32();
                br.ReadInt32(); // ByteRate
                br.ReadInt16(); // BlockAlign
                bitsPerSample = br.ReadInt16();

                // Skip any extra format bytes
                if (chunkSize > 16)
                {
                    br.ReadBytes(chunkSize - 16);
                }
            }
            else if (chunkId == "data")
            {
                var sampleCount = chunkSize / (bitsPerSample / 8);
                samples = new short[sampleCount];

                if (bitsPerSample == 16)
                {
                    for (var i = 0; i < sampleCount; i++)
                    {
                        samples[i] = br.ReadInt16();
                    }
                }
                else if (bitsPerSample == 8)
                {
                    for (var i = 0; i < sampleCount; i++)
                    {
                        var b = br.ReadByte();
                        samples[i] = (short)((b - 128) * 256);
                    }
                }
                else
                {
                    br.ReadBytes(chunkSize);
                }
                break;
            }
            else
            {
                br.ReadBytes(chunkSize);
            }
        }

        return (samples ?? Array.Empty<short>(), sampleRate, channels);
    }
}
