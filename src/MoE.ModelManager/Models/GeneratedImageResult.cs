namespace MoE.ModelManager.Models;

/// <summary>
/// Contains one generated image and local performance data.
/// </summary>
public sealed class GeneratedImageResult
{
    /// <summary>
    /// Gets the encoded image bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Gets the image media type.
    /// </summary>
    public required string MediaType { get; init; }

    /// <summary>
    /// Gets the local output path.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Gets total generation duration in milliseconds.
    /// </summary>
    public double DurationMilliseconds { get; init; }
}
