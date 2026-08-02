namespace MoE.ModelManager.Models;

public sealed class AudioModelDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required string ParakeetModelPath { get; init; }

    public required string KokoroModelPath { get; init; }

    public int NumThreads { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);

    public IReadOnlyList<KokoroVoice> Voices { get; init; } = [];
}
