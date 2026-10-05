namespace Maise.Core.TTS;

public record VoiceInfo(string Id, string Name, string Language, string Region, string Gender)
{
    public override string ToString() => $"{Name} ({Region}) - {Gender}";
}

public static class VoiceManager
{
    public const string DefaultVoiceId = "en-US-heart-kokoro";

    public static readonly IReadOnlyList<VoiceInfo> AllVoices = new List<VoiceInfo>
    {
        // ── US English Voices ──────────────────────────────────────────────────
        new("en-US-heart-kokoro",   "Heart",    "English", "United States", "Female"),
        new("en-US-alloy-kokoro",   "Alloy",    "English", "United States", "Female"),
        new("en-US-aoede-kokoro",   "Aoede",    "English", "United States", "Female"),
        new("en-US-bella-kokoro",   "Bella",    "English", "United States", "Female"),
        new("en-US-jessica-kokoro", "Jessica",  "English", "United States", "Female"),
        new("en-US-kore-kokoro",    "Kore",     "English", "United States", "Female"),
        new("en-US-nicole-kokoro",  "Nicole",   "English", "United States", "Female"),
        new("en-US-nova-kokoro",    "Nova",     "English", "United States", "Female"),
        new("en-US-river-kokoro",   "River",    "English", "United States", "Female"),
        new("en-US-sarah-kokoro",   "Sarah",    "English", "United States", "Female"),
        new("en-US-sky-kokoro",     "Sky",      "English", "United States", "Female"),
        new("en-US-adam-kokoro",    "Adam",     "English", "United States", "Male"),
        new("en-US-echo-kokoro",    "Echo",     "English", "United States", "Male"),
        new("en-US-eric-kokoro",    "Eric",     "English", "United States", "Male"),
        new("en-US-fenrir-kokoro",  "Fenrir",   "English", "United States", "Male"),
        new("en-US-liam-kokoro",    "Liam",     "English", "United States", "Male"),
        new("en-US-michael-kokoro", "Michael",  "English", "United States", "Male"),
        new("en-US-onyx-kokoro",    "Onyx",     "English", "United States", "Male"),
        new("en-US-puck-kokoro",    "Puck",     "English", "United States", "Male"),
        new("en-US-santa-kokoro",   "Santa",    "English", "United States", "Male"),

        // ── British English Voices ──────────────────────────────────────────────
        new("en-GB-alice-kokoro",    "Alice",    "English", "Great Britain", "Female"),
        new("en-GB-emma-kokoro",     "Emma",     "English", "Great Britain", "Female"),
        new("en-GB-isabella-kokoro", "Isabella", "English", "Great Britain", "Female"),
        new("en-GB-lily-kokoro",     "Lily",     "English", "Great Britain", "Female"),
        new("en-GB-daniel-kokoro",   "Daniel",   "English", "Great Britain", "Male"),
        new("en-GB-fable-kokoro",    "Fable",    "English", "Great Britain", "Male"),
        new("en-GB-george-kokoro",   "George",   "English", "Great Britain", "Male"),
        new("en-GB-lewis-kokoro",    "Lewis",    "English", "Great Britain", "Male"),

        // ── German Voices ───────────────────────────────────────────────────────
        new("de-DE-dora-kokoro",     "Dora",     "German",  "Germany",       "Female"),
        new("de-DE-alex-kokoro",     "Alex",     "German",  "Germany",       "Male"),
        new("de-DE-santa-kokoro",    "Santa DE", "German",  "Germany",       "Male"),

        // ── French Voices ───────────────────────────────────────────────────────
        new("fr-FR-siwis-kokoro",    "Siwis",    "French",  "France",        "Female"),

        // ── Greek Voices ────────────────────────────────────────────────────────
        new("el-GR-alpha-f-kokoro",  "Alpha",    "Greek",   "Greece",        "Female"),
        new("el-GR-beta-f-kokoro",   "Beta",     "Greek",   "Greece",        "Female"),
        new("el-GR-omega-m-kokoro",  "Omega",    "Greek",   "Greece",        "Male"),
        new("el-GR-psi-m-kokoro",    "Psi",      "Greek",   "Greece",        "Male"),

        // ── Italian Voices ──────────────────────────────────────────────────────
        new("it-IT-sara-kokoro",     "Sara",     "Italian", "Italy",         "Female"),
        new("it-IT-nicola-kokoro",   "Nicola",   "Italian", "Italy",         "Male"),

        // ── Japanese Voices ─────────────────────────────────────────────────────
        new("ja-JP-alpha-f-kokoro",    "Alpha JP",   "Japanese", "Japan",    "Female"),
        new("ja-JP-gongitsune-kokoro", "Gongitsune", "Japanese", "Japan",    "Female"),
        new("ja-JP-kumo-kokoro",       "Kumo",       "Japanese", "Japan",    "Female"),
        new("ja-JP-nezumi-kokoro",     "Nezumi",     "Japanese", "Japan",    "Male"),
        new("ja-JP-tebukuro-kokoro",   "Tebukuro",   "Japanese", "Japan",    "Male"),

        // ── Portuguese (Brazil) Voices ──────────────────────────────────────────
        new("pt-BR-dora-kokoro",     "Dora BR",  "Portuguese", "Brazil",     "Female"),
        new("pt-BR-alex-kokoro",     "Alex BR",  "Portuguese", "Brazil",     "Male"),
        new("pt-BR-santa-kokoro",    "Santa BR", "Portuguese", "Brazil",     "Male"),

        // ── Chinese Voices ──────────────────────────────────────────────────────
        new("zh-CN-xiaobei-kokoro",  "Xiaobei",  "Chinese",  "China",        "Female"),
        new("zh-CN-xiaoni-kokoro",   "Xiaoni",   "Chinese",  "China",        "Female"),
        new("zh-CN-xiaoxiao-kokoro", "Xiaoxiao", "Chinese",  "China",        "Female"),
        new("zh-CN-xiaoyi-kokoro",   "Xiaoyi",   "Chinese",  "China",        "Female"),
        new("zh-CN-yunjian-kokoro",  "Yunjian",  "Chinese",  "China",        "Male"),
        new("zh-CN-yunxi-kokoro",    "Yunxi",    "Chinese",  "China",        "Male"),
        new("zh-CN-yunxia-kokoro",   "Yunxia",   "Chinese",  "China",        "Male"),
        new("zh-CN-yunyang-kokoro",  "Yunyang",  "Chinese",  "China",        "Male"),
    };

    public static VoiceInfo? FindVoice(string id)
    {
        return AllVoices.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? AllVoices.FirstOrDefault(v => string.Equals(v.Name, id, StringComparison.OrdinalIgnoreCase));
    }
}
