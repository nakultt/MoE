namespace MoE.ModelManager.Models;

public sealed class AudioTranscriptionResult
{
    public required string Text { get; init; }

    public IReadOnlyList<string> Tokens { get; init; } = [];

    public IReadOnlyList<float> Timestamps { get; init; } = [];

    public double DurationMilliseconds { get; init; }

    public double AudioDurationSeconds { get; init; }
}
