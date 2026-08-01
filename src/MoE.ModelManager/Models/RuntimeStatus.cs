namespace MoE.ModelManager.Models;

/// <summary>
/// Identifies the current llama-server lifecycle state.
/// </summary>
public enum RuntimeStatus
{
    /// <summary>No model process is active.</summary>
    Unloaded,

    /// <summary>A model process is starting and loading weights.</summary>
    Loading,

    /// <summary>The model process is ready to accept requests.</summary>
    Ready,

    /// <summary>The model process is shutting down.</summary>
    Ejecting,

    /// <summary>The model process failed.</summary>
    Error,
}
