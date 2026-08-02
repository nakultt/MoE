using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IAudioRuntimeService : IAsyncDisposable
{
    event Action<RuntimeSnapshot>? SpeechToTextStateChanged;

    event Action<RuntimeSnapshot>? TextToSpeechStateChanged;

    RuntimeSnapshot SpeechToTextCurrent { get; }

    RuntimeSnapshot TextToSpeechCurrent { get; }

    IReadOnlyList<KokoroVoice> Voices { get; }

    Task LoadSpeechToTextAsync(AudioModelDefinition model, CancellationToken cancellationToken);

    Task LoadTextToSpeechAsync(AudioModelDefinition model, CancellationToken cancellationToken);

    Task EjectSpeechToTextAsync(CancellationToken cancellationToken);

    Task EjectTextToSpeechAsync(CancellationToken cancellationToken);

    Task<AudioTranscriptionResult> TranscribeAsync(string wavePath, CancellationToken cancellationToken);

    Task<GeneratedSpeechResult> SynthesizeAsync(
        string text,
        KokoroVoice voice,
        float speed,
        CancellationToken cancellationToken);
}
