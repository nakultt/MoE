namespace MoE.ModelManager.Models;

public sealed class GeneratedSpeechResult
{
    public required string OutputPath { get; init; }

    public double DurationMilliseconds { get; init; }

    public double AudioDurationSeconds { get; init; }

    public int SampleRate { get; init; }
}
