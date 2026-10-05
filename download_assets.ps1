# Maise for Windows - Asset Download Script
$ErrorActionPreference = "Stop"

$assetsDir = Join-Path $PSScriptRoot "assets"
$ttsDir = Join-Path $assetsDir "tts"
$asrDir = Join-Path $assetsDir "asr"
$voicesDir = Join-Path $ttsDir "voices"

New-Item -ItemType Directory -Force -Path $ttsDir, $asrDir, $voicesDir | Out-Null

$baseUrl = "https://raw.githubusercontent.com/Mobile-Artificial-Intelligence/maise/main"

$files = @(
    @{ Url = "$baseUrl/kokoro/src/main/assets/kokoro-quantized.onnx"; Path = (Join-Path $ttsDir "kokoro-quantized.onnx") },
    @{ Url = "$baseUrl/open-phonemizer/src/main/assets/open-phonemizer.onnx"; Path = (Join-Path $ttsDir "open-phonemizer.onnx") },
    @{ Url = "$baseUrl/open-phonemizer/src/main/assets/dictionary.json"; Path = (Join-Path $ttsDir "dictionary.json") },
    @{ Url = "$baseUrl/whisper/src/main/assets/encoder_model_quantized.onnx"; Path = (Join-Path $asrDir "encoder_model_quantized.onnx") },
    @{ Url = "$baseUrl/whisper/src/main/assets/decoder_model_quantized.onnx"; Path = (Join-Path $asrDir "decoder_model_quantized.onnx") },
    @{ Url = "$baseUrl/whisper/src/main/assets/vocab.json"; Path = (Join-Path $asrDir "vocab.json") }
)

$voices = @(
    "de-DE-alex-kokoro.bin", "de-DE-dora-kokoro.bin", "de-DE-santa-kokoro.bin",
    "el-GR-alpha-f-kokoro.bin", "el-GR-beta-f-kokoro.bin", "el-GR-omega-m-kokoro.bin", "el-GR-psi-m-kokoro.bin",
    "en-GB-alice-kokoro.bin", "en-GB-daniel-kokoro.bin", "en-GB-emma-kokoro.bin", "en-GB-fable-kokoro.bin",
    "en-GB-george-kokoro.bin", "en-GB-isabella-kokoro.bin", "en-GB-lewis-kokoro.bin", "en-GB-lily-kokoro.bin",
    "en-US-adam-kokoro.bin", "en-US-alloy-kokoro.bin", "en-US-aoede-kokoro.bin", "en-US-bella-kokoro.bin",
    "en-US-echo-kokoro.bin", "en-US-eric-kokoro.bin", "en-US-fenrir-kokoro.bin", "en-US-heart-kokoro.bin",
    "en-US-jessica-kokoro.bin", "en-US-kore-kokoro.bin", "en-US-liam-kokoro.bin", "en-US-michael-kokoro.bin",
    "en-US-nicole-kokoro.bin", "en-US-nova-kokoro.bin", "en-US-onyx-kokoro.bin", "en-US-puck-kokoro.bin",
    "en-US-river-kokoro.bin", "en-US-santa-kokoro.bin", "en-US-sarah-kokoro.bin", "en-US-sky-kokoro.bin",
    "fr-FR-siwis-kokoro.bin", "it-IT-nicola-kokoro.bin", "it-IT-sara-kokoro.bin",
    "ja-JP-alpha-f-kokoro.bin", "ja-JP-gongitsune-kokoro.bin", "ja-JP-kumo-kokoro.bin", "ja-JP-nezumi-kokoro.bin", "ja-JP-tebukuro-kokoro.bin",
    "pt-BR-alex-kokoro.bin", "pt-BR-dora-kokoro.bin", "pt-BR-santa-kokoro.bin",
    "zh-CN-xiaobei-kokoro.bin", "zh-CN-xiaoni-kokoro.bin", "zh-CN-xiaoxiao-kokoro.bin", "zh-CN-xiaoyi-kokoro.bin",
    "zh-CN-yunjian-kokoro.bin", "zh-CN-yunxi-kokoro.bin", "zh-CN-yunxia-kokoro.bin", "zh-CN-yunyang-kokoro.bin"
)

foreach ($v in $voices) {
    $files += @{ Url = "$baseUrl/kokoro/src/main/assets/voices/$v"; Path = (Join-Path $voicesDir $v) }
}

Write-Host "Checking / Downloading Maise ONNX models and assets..." -ForegroundColor Cyan

foreach ($f in $files) {
    $name = [System.IO.Path]::GetFileName($f.Path)
    if (Test-Path $f.Path) {
        Write-Host " [OK] $name already exists." -ForegroundColor Green
    } else {
        Write-Host " [DOWNLOADING] $name..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $f.Url -OutFile $f.Path -UseBasicParsing
    }
}

Write-Host "All assets ready!" -ForegroundColor Green
