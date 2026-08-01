namespace MoE.ModelManager.Models;

/// <summary>
/// Captures the observable state of the managed llama-server process.
/// </summary>
public sealed class RuntimeSnapshot
{
    /// <summary>Gets the lifecycle state.</summary>
    public RuntimeStatus Status { get; init; }

    /// <summary>Gets the loaded model identifier, when available.</summary>
    public string? ModelId { get; init; }

    /// <summary>Gets the latest diagnostic detail.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets the managed process identifier, when active.</summary>
    public int? ProcessId { get; init; }
}
