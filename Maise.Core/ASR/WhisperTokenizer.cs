using System.Text;
using System.Text.Json;

namespace Maise.Core.ASR;

public class WhisperTokenizer
{
    public const int TokenEot = 50256;          // <|endoftext|>
    public const int TokenSot = 50257;          // <|startoftranscript|>
    public const int TokenEn = 50258;           // <|en|>
    public const int TokenTranscribe = 50358;   // <|transcribe|>
    public const int TokenNoTimestamps = 50362; // <|notimestamps|>

    private readonly Dictionary<int, string> _idToToken;
    private readonly Dictionary<char, byte> _unicodeToByte;

    public WhisperTokenizer(string vocabPath)
    {
        if (!File.Exists(vocabPath))
            throw new FileNotFoundException($"Whisper vocab file not found: {vocabPath}");

        _idToToken = LoadVocab(vocabPath);
        _unicodeToByte = BuildUnicodeToByte();
    }

    private static Dictionary<int, string> LoadVocab(string vocabPath)
    {
        using var fs = File.OpenRead(vocabPath);
        using var doc = JsonDocument.Parse(fs);
        var map = new Dictionary<int, string>(doc.RootElement.EnumerateObject().Count());

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            map[prop.Value.GetInt32()] = prop.Name;
        }
        return map;
    }

    public string Decode(IEnumerable<int> tokenIds)
    {
        var bytes = new List<byte>();
        foreach (var id in tokenIds)
        {
            if (id >= TokenEot) continue;
            if (!_idToToken.TryGetValue(id, out var tokenStr)) continue;

            foreach (var ch in tokenStr)
            {
                if (_unicodeToByte.TryGetValue(ch, out var b))
                {
                    bytes.Add(b);
                }
                else
                {
                    var utfBytes = Encoding.UTF8.GetBytes(ch.ToString());
                    bytes.AddRange(utfBytes);
                }
            }
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public bool IsWordStart(int tokenId)
    {
        if (!_idToToken.TryGetValue(tokenId, out var tokenStr) || tokenStr.Length == 0)
            return false;

        if (_unicodeToByte.TryGetValue(tokenStr[0], out var firstByte))
        {
            return firstByte == (byte)' ';
        }
        return false;
    }

    public static Dictionary<char, byte> BuildUnicodeToByte()
    {
        var bs = new List<int>();
        for (var b = 33; b <= 126; b++) bs.Add(b);
        for (var b = 161; b <= 172; b++) bs.Add(b);
        for (var b = 174; b <= 255; b++) bs.Add(b);

        var cs = new List<int>(bs);
        var n = 0;
        for (var b = 0; b <= 255; b++)
        {
            if (!bs.Contains(b))
            {
                bs.Add(b);
                cs.Add(256 + n);
                n++;
            }
        }

        var result = new Dictionary<char, byte>(256);
        for (var i = 0; i < bs.Count; i++)
        {
            result[(char)cs[i]] = (byte)bs[i];
        }
        return result;
    }
}
