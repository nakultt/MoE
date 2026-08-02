namespace MoE.ModelManager.Models;

/// <summary>
/// Contains one generated video and local performance data.
/// </summary>
public sealed class GeneratedVideoResult
{
    /// <summary>
    /// Gets the FastVideo job identifier.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the local MP4 path.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Gets total generation duration in milliseconds.
    /// </summary>
    public double DurationMilliseconds { get; init; }
}
