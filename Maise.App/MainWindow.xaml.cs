using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Maise.Core;
using Maise.Core.ASR;
using Maise.Core.Audio;
using Maise.Core.TTS;

namespace Maise.App;

public partial class MainWindow : Window
{
    private readonly MaiseEngine _engine;
    private HotKeyManager? _hotKeyManager;
    private bool _isRecording;
    private bool _isServerRunning;
    private float _speed = 1.0f;

    public MainWindow()
    {
        InitializeComponent();
        _engine = new MaiseEngine();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate Voice List
        VoiceComboBox.ItemsSource = VoiceManager.AllVoices;
        VoiceComboBox.SelectedItem = VoiceManager.AllVoices.FirstOrDefault(v => v.Id == VoiceManager.DefaultVoiceId)
            ?? VoiceManager.AllVoices.FirstOrDefault();

        // Wire recorder audio level meter
        _engine.Recorder.AudioLevelChanged += level =>
        {
            Dispatcher.Invoke(() => AudioLevelBar.Value = level);
        };

        // Initialize Hotkeys
        try
        {
            _hotKeyManager = new HotKeyManager(this);
            // Win (0x0008) + Alt (0x0001) + 'S' (0x53) -> Speak Clipboard
            _hotKeyManager.Register(0x0008 | 0x0001, 0x53, OnHotkeySpeakClipboard);
            // Win (0x0008) + Alt (0x0001) + 'D' (0x44) -> Dictation
            _hotKeyManager.Register(0x0008 | 0x0001, 0x44, OnHotkeyDictation);
        }
        catch (Exception ex)
        {
            GlobalStatusText.Text = $"Hotkeys note: {ex.Message}";
        }

        // Initialize TTS engine in background so UI opens instantly
        GlobalStatusText.Text = "Initializing speech engines on CPU...";
        await Task.Run(() =>
        {
            try
            {
                _engine.InitializeTts();
                _engine.InitializeAsr();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => GlobalStatusText.Text = $"Init warning: {ex.Message}");
            }
        });
        GlobalStatusText.Text = "Maise Speech Engine Ready (CPU AVX)";
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _speed = (float)e.NewValue;
        if (SpeedLabel != null)
        {
            SpeedLabel.Text = $"{_speed:F1}x";
        }
    }

    private async void SpeakButton_Click(object sender, RoutedEventArgs e)
    {
        var text = TtsInputText.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var selectedVoice = (VoiceComboBox.SelectedItem as VoiceInfo)?.Id ?? VoiceManager.DefaultVoiceId;

        SpeakButton.IsEnabled = false;
        GlobalStatusText.Text = $"Synthesizing using {selectedVoice}...";
        TtsStatusDetails.Text = "Synthesizing audio...";

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var samples = await Task.Run(() => _engine.Tts!.Synthesize(text, selectedVoice, _speed));
            sw.Stop();

            var audioSeconds = (double)samples.Length / KokoroTTS.SampleRate;
            TtsStatusDetails.Text = $"Synthesized {audioSeconds:F2}s audio in {sw.ElapsedMilliseconds}ms. Playing...";
            GlobalStatusText.Text = "Playing audio...";

            _engine.Player.PlaybackFinished += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    GlobalStatusText.Text = "Playback finished.";
                    SpeakButton.IsEnabled = true;
                });
            };

            _engine.Player.Play(samples, KokoroTTS.SampleRate, 1);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Speech synthesis error:\n{ex.Message}", "TTS Error", MessageBoxButton.OK, MessageBoxImage.Error);
            GlobalStatusText.Text = "Synthesis failed.";
            SpeakButton.IsEnabled = true;
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _engine.StopSpeaking();
        SpeakButton.IsEnabled = true;
        GlobalStatusText.Text = "Playback stopped.";
    }

    private async void SaveWavButton_Click(object sender, RoutedEventArgs e)
    {
        var text = TtsInputText.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var selectedVoice = (VoiceComboBox.SelectedItem as VoiceInfo)?.Id ?? VoiceManager.DefaultVoiceId;

        var sfd = new SaveFileDialog
        {
            Filter = "WAV Audio File (*.wav)|*.wav",
            FileName = "speech.wav"
        };

        if (sfd.ShowDialog() == true)
        {
            GlobalStatusText.Text = "Synthesizing and saving WAV...";
            try
            {
                var samples = await Task.Run(() => _engine.Tts!.Synthesize(text, selectedVoice, _speed));
                WavUtility.SaveWavFile(sfd.FileName, samples, KokoroTTS.SampleRate, 1);
                GlobalStatusText.Text = $"Saved to: {Path.GetFileName(sfd.FileName)}";
                MessageBox.Show($"WAV file successfully saved to:\n{sfd.FileName}", "Saved Audio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving WAV:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRecording)
        {
            _isRecording = true;
            RecordButton.Content = "⏹ Stop & Transcribe";
            RecordButton.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red
            AsrStatusDetails.Text = "Recording audio from microphone...";
            GlobalStatusText.Text = "Recording microphone...";
            _engine.Recorder.StartRecording(WhisperASR.SampleRate);
        }
        else
        {
            _isRecording = false;
            RecordButton.Content = "⏳ Processing...";
            RecordButton.IsEnabled = false;
            AudioLevelBar.Value = 0;

            var samples = _engine.Recorder.StopRecording();
            AsrStatusDetails.Text = $"Transcribing {samples.Length / (double)WhisperASR.SampleRate:F1}s audio with Distil-Whisper...";
            GlobalStatusText.Text = "Transcribing with Whisper...";

            try
            {
                var transcript = await Task.Run(() => _engine.Asr!.Transcribe(samples, WhisperASR.SampleRate, partial =>
                {
                    Dispatcher.Invoke(() => AsrOutputText.Text = partial);
                }));

                AsrOutputText.Text = transcript;
                AsrStatusDetails.Text = "Transcription completed.";
                GlobalStatusText.Text = "Transcription complete.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transcription error:\n{ex.Message}", "ASR Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RecordButton.Content = "🔴 Hold / Click to Record";
                RecordButton.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                RecordButton.IsEnabled = true;
            }
        }
    }

    private async void TranscribeFileButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav)|*.wav|All Files (*.*)|*.*",
            Title = "Select Audio File to Transcribe"
        };

        if (ofd.ShowDialog() == true)
        {
            GlobalStatusText.Text = $"Transcribing {Path.GetFileName(ofd.FileName)}...";
            AsrStatusDetails.Text = "Loading audio file...";

            try
            {
                var bytes = await File.ReadAllBytesAsync(ofd.FileName);
                var (samples, sampleRate, _) = WavUtility.ReadWav(bytes);

                var transcript = await Task.Run(() => _engine.Asr!.Transcribe(samples, sampleRate, partial =>
                {
                    Dispatcher.Invoke(() => AsrOutputText.Text = partial);
                }));

                AsrOutputText.Text = transcript;
                AsrStatusDetails.Text = "Transcription finished.";
                GlobalStatusText.Text = "File transcribed successfully.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading/transcribing audio:\n{ex.Message}", "Transcription Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CopyTranscriptButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(AsrOutputText.Text))
        {
            Clipboard.SetText(AsrOutputText.Text);
            GlobalStatusText.Text = "Transcript copied to clipboard.";
        }
    }

    private void ToggleServerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isServerRunning)
        {
            try
            {
                _engine.StartServer(8033);
                _isServerRunning = true;
                ToggleServerButton.Content = "Stop API Server";
                ToggleServerButton.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                ServerStatusText.Text = "API: Running (:8033)";
                ServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                GlobalStatusText.Text = "API server listening on http://127.0.0.1:8033/";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start server:\n{ex.Message}", "Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            _engine.StopServer();
            _isServerRunning = false;
            ToggleServerButton.Content = "Start API Server";
            ToggleServerButton.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            ServerStatusText.Text = "API: Stopped";
            ServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            GlobalStatusText.Text = "API server stopped.";
        }
    }

    private void OnHotkeySpeakClipboard()
    {
        var text = Clipboard.GetText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var voice = (VoiceComboBox.SelectedItem as VoiceInfo)?.Id ?? VoiceManager.DefaultVoiceId;
            Task.Run(() => _engine.Speak(text, voice, _speed));
        }
    }

    private void OnHotkeyDictation()
    {
        Dispatcher.Invoke(() =>
        {
            RecordButton_Click(RecordButton, new RoutedEventArgs());
        });
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (MinimizeToTrayCheckBox.IsChecked == true)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _hotKeyManager?.Dispose();
        _engine.Dispose();
    }
}