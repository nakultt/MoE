using System.Globalization;

namespace MoE.ModelManager.Models;

/// <summary>
/// Defines a GPU-conscious video generation preset.
/// </summary>
public sealed class VideoGenerationPreset
{
    /// <summary>
    /// Gets the name shown in the preset picker.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the output width.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the output height.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the native frame count.
    /// </summary>
    public int Frames { get; init; }

    /// <summary>
    /// Gets the output frame rate.
    /// </summary>
    public int FramesPerSecond { get; init; }

    /// <summary>
    /// Gets the distilled denoising step count.
    /// </summary>
    public int Steps { get; init; } = 3;

    /// <summary>
    /// Gets the OpenAI-compatible size value.
    /// </summary>
    public string Size => string.Create(
        CultureInfo.InvariantCulture,
        $"{Width}x{Height}");
}
