namespace MoE.ModelManager.Models;

/// <summary>
/// Describes one local video generation model and its WSL runtime.
/// </summary>
public sealed class VideoModelDefinition
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
    /// Gets the Windows directory containing the Diffusers model.
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Gets the WSL distribution used to host FastVideo.
    /// </summary>
    public string WslDistribution { get; init; } = "Ubuntu";

    /// <summary>
    /// Gets the FastVideo executable path inside WSL.
    /// </summary>
    public string FastVideoExecutable { get; init; } = "/home/nakul/.venvs/moe-fastvideo/bin/fastvideo";

    /// <summary>
    /// Gets the model startup timeout in seconds.
    /// </summary>
    public int StartupTimeoutSeconds { get; init; } = 1800;
}
