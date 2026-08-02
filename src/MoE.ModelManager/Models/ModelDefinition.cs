namespace MoE.ModelManager.Models;

/// <summary>
/// Describes one local GGUF model and its measured llama.cpp configuration.
/// </summary>
public sealed class ModelDefinition
{
    /// <summary>
    /// Gets the stable OpenAI API model identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the model name shown in the user interface.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the short model description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the absolute target GGUF path.
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Gets the configured context size.
    /// </summary>
    public int ContextSize { get; init; }

    /// <summary>
    /// Gets the number of expert layers kept on the CPU.
    /// </summary>
    public int CpuMoeLayers { get; init; }

    /// <summary>
    /// Gets the KV cache quantization type passed to llama.cpp.
    /// </summary>
    public required string CacheType { get; init; }

    /// <summary>
    /// Gets the number of CPU inference threads.
    /// </summary>
    public int Threads { get; init; }

    /// <summary>
    /// Gets the optional MTP draft model path.
    /// </summary>
    public string? DraftModelPath { get; init; }

    /// <summary>
    /// Gets the maximum MTP draft length.
    /// </summary>
    public int DraftTokens { get; init; }

    /// <summary>
    /// Gets the measured sustained generation speed.
    /// </summary>
    public double MeasuredTokensPerSecond { get; init; }

    /// <summary>
    /// Gets a value indicating whether MTP speculative decoding is configured.
    /// </summary>
    public bool HasDraftModel => !string.IsNullOrWhiteSpace(DraftModelPath) && DraftTokens > 0;
}
