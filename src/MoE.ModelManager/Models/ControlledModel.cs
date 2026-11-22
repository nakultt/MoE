namespace MoE.ModelManager.Models;

/// <summary>
/// Describes one model that the control API can load or eject, with its live runtime state.
/// </summary>
/// <param name="Id">The stable model identifier used by the OpenAI-compatible API.</param>
/// <param name="Name">The model display name.</param>
/// <param name="Kind">The runtime that serves the model.</param>
/// <param name="Status">The current runtime state for this model.</param>
/// <param name="Detail">The latest diagnostic detail, when available.</param>
public sealed record ControlledModel(
    string Id,
    string Name,
    ModelKind Kind,
    RuntimeStatus Status,
    string? Detail);
