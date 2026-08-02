namespace MoE.ModelManager.Models;

/// <summary>
/// Describes one local image generation model and its runtime configuration.
/// </summary>
public sealed class ImageModelDefinition
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
    /// Gets the diffusion transformer weights path.
    /// </summary>
    public required string DiffusionModelPath { get; init; }

    /// <summary>
    /// Gets the text encoder weights path.
    /// </summary>
    public required string TextEncoderPath { get; init; }

    /// <summary>
    /// Gets the VAE weights path.
    /// </summary>
    public required string VaePath { get; init; }

    /// <summary>
    /// Gets the distilled sampling step count.
    /// </summary>
    public int Steps { get; init; } = 4;

    /// <summary>
    /// Gets the CPU worker count.
    /// </summary>
    public int Threads { get; init; } = 14;
}
