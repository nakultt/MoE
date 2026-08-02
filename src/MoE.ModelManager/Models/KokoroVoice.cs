namespace MoE.ModelManager.Models;

public sealed class KokoroVoice
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public int SpeakerId { get; init; }

    public override string ToString() => DisplayName;
}
