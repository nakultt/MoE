using System.Diagnostics;
using MoE.ModelManager.Models;
using SherpaOnnx;

namespace MoE.ModelManager.Services;

internal sealed class AudioRuntimeService : IAudioRuntimeService
{
    private readonly SemaphoreSlim _speechToTextGate = new(1, 1);
    private readonly SemaphoreSlim _textToSpeechGate = new(1, 1);
    private OfflineRecognizer? _recognizer;
    private OfflineTts? _tts;
    private bool _disposed;

    public event Action<RuntimeSnapshot>? SpeechToTextStateChanged;

    public event Action<RuntimeSnapshot>? TextToSpeechStateChanged;

    public RuntimeSnapshot SpeechToTextCurrent { get; private set; } = new()
    {
        Status = RuntimeStatus.Unloaded,
    };

    public RuntimeSnapshot TextToSpeechCurrent { get; private set; } = new()
    {
        Status = RuntimeStatus.Unloaded,
    };

    public IReadOnlyList<KokoroVoice> Voices { get; private set; } = [];

    public async Task LoadSpeechToTextAsync(AudioModelDefinition model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _speechToTextGate.WaitAsync(cancellationToken);
        try
        {
            DisposeRecognizer();
            ValidateSpeechToTextFiles(model);
            UpdateSpeechToTextState(RuntimeStatus.Loading, model.Id, null);
            _recognizer = await Task.Run(() => CreateRecognizer(model), cancellationToken);
            UpdateSpeechToTextState(RuntimeStatus.Ready, model.Id, null);
        }
        catch (Exception exception)
        {
            DisposeRecognizer();
            UpdateSpeechToTextState(RuntimeStatus.Error, model.Id, exception.Message);
            throw;
        }
        finally
        {
            _speechToTextGate.Release();
        }
    }

    public async Task LoadTextToSpeechAsync(AudioModelDefinition model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _textToSpeechGate.WaitAsync(cancellationToken);
        try
        {
            DisposeTextToSpeech();
            ValidateTextToSpeechFiles(model);
            UpdateTextToSpeechState(RuntimeStatus.Loading, model.Id, null);
            _tts = await Task.Run(() => CreateTextToSpeech(model), cancellationToken);
            Voices = model.Voices;
            UpdateTextToSpeechState(RuntimeStatus.Ready, model.Id, null);
        }
        catch (Exception exception)
        {
            DisposeTextToSpeech();
            UpdateTextToSpeechState(RuntimeStatus.Error, model.Id, exception.Message);
            throw;
        }
        finally
        {
            _textToSpeechGate.Release();
        }
    }

    public async Task EjectSpeechToTextAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _speechToTextGate.WaitAsync(cancellationToken);
        try
        {
            if (_recognizer is not null)
            {
                UpdateSpeechToTextState(RuntimeStatus.Ejecting, SpeechToTextCurrent.ModelId, null);
            }

            DisposeRecognizer();
            UpdateSpeechToTextState(RuntimeStatus.Unloaded, null, null);
        }
        finally
        {
            _speechToTextGate.Release();
        }
    }

    public async Task EjectTextToSpeechAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _textToSpeechGate.WaitAsync(cancellationToken);
        try
        {
            if (_tts is not null)
            {
                UpdateTextToSpeechState(RuntimeStatus.Ejecting, TextToSpeechCurrent.ModelId, null);
            }

            DisposeTextToSpeech();
            UpdateTextToSpeechState(RuntimeStatus.Unloaded, null, null);
        }
        finally
        {
            _textToSpeechGate.Release();
        }
    }

    public async Task<AudioTranscriptionResult> TranscribeAsync(
        string wavePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavePath);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _speechToTextGate.WaitAsync(cancellationToken);
        try
        {
            OfflineRecognizer recognizer = _recognizer
                ?? throw new InvalidOperationException("The speech-to-text model is not loaded.");
            return await Task.Run(
                () => TranscribeCore(recognizer, wavePath, cancellationToken),
                cancellationToken);
        }
        finally
        {
            _speechToTextGate.Release();
        }
    }

    public async Task<GeneratedSpeechResult> SynthesizeAsync(
        string text,
        KokoroVoice voice,
        float speed,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(voice);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _textToSpeechGate.WaitAsync(cancellationToken);
        try
        {
            OfflineTts tts = _tts ?? throw new InvalidOperationException("The text-to-speech model is not loaded.");
            return await Task.Run(
                () => SynthesizeCore(tts, text, voice, speed, cancellationToken),
                cancellationToken);
        }
        finally
        {
            _textToSpeechGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await EjectSpeechToTextAsync(CancellationToken.None);
        await EjectTextToSpeechAsync(CancellationToken.None);
        _disposed = true;
        _speechToTextGate.Dispose();
        _textToSpeechGate.Dispose();
    }

    internal void StopImmediately()
    {
        DisposeRecognizer();
        DisposeTextToSpeech();
    }

    private static OfflineRecognizer CreateRecognizer(AudioModelDefinition model)
    {
        OfflineRecognizerConfig config = new();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = Path.Combine(model.ParakeetModelPath, "tokens.txt");
        config.ModelConfig.Transducer.Encoder = Path.Combine(model.ParakeetModelPath, "encoder.int8.onnx");
        config.ModelConfig.Transducer.Decoder = Path.Combine(model.ParakeetModelPath, "decoder.int8.onnx");
        config.ModelConfig.Transducer.Joiner = Path.Combine(model.ParakeetModelPath, "joiner.int8.onnx");
        config.ModelConfig.ModelType = "nemo_transducer";
        config.ModelConfig.NumThreads = model.NumThreads;
        config.ModelConfig.Provider = "cpu";
        config.DecodingMethod = "greedy_search";
        return new OfflineRecognizer(config);
    }

    private static OfflineTts CreateTextToSpeech(AudioModelDefinition model)
    {
        OfflineTtsConfig config = new();
        config.Model.NumThreads = model.NumThreads;
        config.Model.Provider = "cpu";
        config.Model.Kokoro.Model = Path.Combine(model.KokoroModelPath, "model.onnx");
        config.Model.Kokoro.Voices = Path.Combine(model.KokoroModelPath, "voices.bin");
        config.Model.Kokoro.Tokens = Path.Combine(model.KokoroModelPath, "tokens.txt");
        config.Model.Kokoro.DataDir = Path.Combine(model.KokoroModelPath, "espeak-ng-data");
        config.Model.Kokoro.DictDir = Path.Combine(model.KokoroModelPath, "dict");
        config.Model.Kokoro.Lexicon = Path.Combine(model.KokoroModelPath, "lexicon-us-en.txt");
        config.Model.Kokoro.Lang = "en-us";
        return new OfflineTts(config);
    }

    private static AudioTranscriptionResult TranscribeCore(
        OfflineRecognizer recognizer,
        string wavePath,
        CancellationToken cancellationToken)
    {
        WaveAudioData audio = WaveAudioReader.Read(wavePath);
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch stopwatch = Stopwatch.StartNew();
        using OfflineStream stream = recognizer.CreateStream();
        stream.AcceptWaveform(audio.SampleRate, audio.Samples);
        recognizer.Decode(stream);
        cancellationToken.ThrowIfCancellationRequested();
        OfflineRecognizerResult result = stream.Result;
        stopwatch.Stop();
        return new AudioTranscriptionResult
        {
            Text = result.Text,
            Tokens = result.Tokens,
            Timestamps = result.Timestamps,
            DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            AudioDurationSeconds = audio.DurationSeconds,
        };
    }

    private static GeneratedSpeechResult SynthesizeCore(
        OfflineTts tts,
        string text,
        KokoroVoice voice,
        float speed,
        CancellationToken cancellationToken)
    {
        string outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Audio");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"speech-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.wav");
        OfflineTtsGenerationConfig generationConfig = new()
        {
            Sid = voice.SpeakerId,
            Speed = Math.Clamp(speed, 0.5f, 2f),
            SilenceScale = 0.2f,
        };

        Stopwatch stopwatch = Stopwatch.StartNew();
        OfflineTtsGeneratedAudio audio = tts.GenerateWithConfig(text, generationConfig, null!);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!audio.SaveToWaveFile(outputPath))
            {
                throw new IOException("Kokoro could not save the generated WAV file.");
            }

            stopwatch.Stop();
            return new GeneratedSpeechResult
            {
                OutputPath = outputPath,
                DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                AudioDurationSeconds = audio.NumSamples / (double)audio.SampleRate,
                SampleRate = audio.SampleRate,
            };
        }
        finally
        {
            audio.Dispose();
        }
    }

    private static void ValidateSpeechToTextFiles(AudioModelDefinition model)
    {
        string[] requiredFiles =
        [
            Path.Combine(model.ParakeetModelPath, "encoder.int8.onnx"),
            Path.Combine(model.ParakeetModelPath, "decoder.int8.onnx"),
            Path.Combine(model.ParakeetModelPath, "joiner.int8.onnx"),
            Path.Combine(model.ParakeetModelPath, "tokens.txt"),
        ];
        ValidateFiles(requiredFiles, "speech-to-text");
    }

    private static void ValidateTextToSpeechFiles(AudioModelDefinition model)
    {
        string[] requiredFiles =
        [
            Path.Combine(model.KokoroModelPath, "model.onnx"),
            Path.Combine(model.KokoroModelPath, "voices.bin"),
            Path.Combine(model.KokoroModelPath, "tokens.txt"),
            Path.Combine(model.KokoroModelPath, "lexicon-us-en.txt"),
        ];
        ValidateFiles(requiredFiles, "text-to-speech");
    }

    private static void ValidateFiles(IEnumerable<string> requiredFiles, string modelType)
    {
        string? missing = requiredFiles.FirstOrDefault(path => !File.Exists(path));
        if (missing is not null)
        {
            throw new FileNotFoundException(
                $"A {modelType} model file is missing. Run the audio model setup first.",
                missing);
        }
    }

    private void DisposeRecognizer()
    {
        _recognizer?.Dispose();
        _recognizer = null;
    }

    private void DisposeTextToSpeech()
    {
        _tts?.Dispose();
        _tts = null;
        Voices = [];
    }

    private void UpdateSpeechToTextState(RuntimeStatus status, string? modelId, string? detail)
    {
        SpeechToTextCurrent = new RuntimeSnapshot
        {
            Status = status,
            ModelId = modelId,
            Detail = detail,
        };
        SpeechToTextStateChanged?.Invoke(SpeechToTextCurrent);
    }

    private void UpdateTextToSpeechState(RuntimeStatus status, string? modelId, string? detail)
    {
        TextToSpeechCurrent = new RuntimeSnapshot
        {
            Status = status,
            ModelId = modelId,
            Detail = detail,
        };
        TextToSpeechStateChanged?.Invoke(TextToSpeechCurrent);
    }
}
