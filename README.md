# Maise for Windows 🎙️🗣️

> **On-Device Neural Speech Engine for Windows**  
> Ported from Android [Mobile-Artificial-Intelligence/maise](https://github.com/Mobile-Artificial-Intelligence/maise) to native Windows (.NET 8 + ONNX Runtime).

Maise brings local, edge-native neural speech synthesis (**Kokoro TTS**) and automatic speech recognition (**Distil-Whisper ASR**) to Windows PCs. It runs 100% offline and on-device on standard CPUs (optimized for AVX without requiring AVX2 or discrete GPUs).

---

## 🚀 Key Features

- 🗣️ **Kokoro Neural TTS (24 kHz)**:
  - Generates studio-quality, natural human-sounding speech waveforms.
  - Sentence-level streaming synthesis.
  - Supports 54+ voices across multiple accents (US, UK, DE, FR, IT, EL, JA, BR, ZH).
- 🎙️ **Distil-Whisper ASR (16 kHz)**:
  - Fast, accurate English speech-to-text with 80-band Slaney Mel spectrogram extraction.
  - Streaming word-level partial decoding.
- 💻 **Modern Windows Desktop App (`Maise.App`)**:
  - Dark-themed UI with text synthesis studio, microphone dictation, and file transcriber.
  - **Global Hotkeys**:
    - `Win + Alt + S`: Highlight any text in Windows and speak aloud instantly.
    - `Win + Alt + D`: Push-to-talk voice dictation directly into any active application.
  - **System Tray**: Minimizes to background tray.
- 🌐 **OpenAI-Compatible Local REST Server**:
  - Host a local server on port `8033` compatible with standard AI clients and browser extensions:
    - `POST /v1/audio/speech`
    - `POST /v1/audio/transcriptions`
    - `GET  /v1/audio/voices`
    - `GET  /health`
- ⚡ **CLI Tool (`Maise.Cli`)**:
  - Scriptable command-line interface for text-to-speech and transcription in automated workflows.

---

## 🏗️ Architecture

```
Maise (Windows Port)
├── Maise.Core/
│   ├── Text/              # Normalizer (numbers, ordinals, abbreviations)
│   ├── TTS/               # OpenPhonemizer + Kokoro ONNX Inference Engine + Voice Manager
│   ├── ASR/               # FFT, Slaney Filterbank + Distil-Whisper ONNX Engine + Tokenizer
│   ├── Audio/             # NAudio WASAPI Player, Mic Recorder & WAV IO
│   └── Server/            # OpenAI-compatible REST API Server
├── Maise.App/             # WPF Desktop UI + System Tray + Global Hotkeys
├── Maise.Cli/             # Command line utility
├── Maise.Tests/           # Unit & Integration test suite
└── assets/                # Quantized ONNX models, vocab, and voice vectors
```

---

## 🛠️ Getting Started

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Building the Project
```powershell
# Restore dependencies and build solution
dotnet build
```

### Running the Desktop App
```powershell
dotnet run --project Maise.App/Maise.App.csproj
```

### Running CLI Commands
```powershell
# Speak text aloud
dotnet run --project Maise.Cli/Maise.Cli.csproj -- tts "Hello world from Maise on Windows!" --play

# Synthesize to WAV file
dotnet run --project Maise.Cli/Maise.Cli.csproj -- tts "Saving offline speech." --output output.wav

# Transcribe an audio file
dotnet run --project Maise.Cli/Maise.Cli.csproj -- asr output.wav

# Start the Local OpenAI-compatible REST server
dotnet run --project Maise.Cli/Maise.Cli.csproj -- server --port 8033
```

### Running Tests
```powershell
dotnet test
```

---

## 🌐 Local API Reference

### 1. Synthesize Speech (`POST /v1/audio/speech`)
```bash
curl -X POST http://127.0.0.1:8033/v1/audio/speech \
  -H "Content-Type: application/json" \
  -d '{
    "input": "Welcome to local neural text to speech!",
    "voice": "en-US-heart-kokoro",
    "speed": 1.0
  }' \
  --output speech.wav
```

### 2. Transcribe Audio (`POST /v1/audio/transcriptions`)
```bash
curl -X POST http://127.0.0.1:8033/v1/audio/transcriptions \
  --data-binary "@speech.wav"
```
**Response:**
```json
{
  "text": "Welcome to local neural text to speech!"
}
```

---

## 📜 License
MIT License. Based on [Mobile-Artificial-Intelligence/maise](https://github.com/Mobile-Artificial-Intelligence/maise).
