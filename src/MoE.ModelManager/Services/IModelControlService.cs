using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Loads and ejects models without the user interface, for the local control API.
/// </summary>
internal interface IModelControlService
{
    /// <summary>
    /// Raised when a client asks the app to exit.
    /// </summary>
    event Action? ShutdownRequested;

    /// <summary>
    /// Lists every configured model with its live runtime state.
    /// </summary>
    Task<IReadOnlyList<ControlledModel>> GetModelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds one configured model by identifier, or returns <see langword="null"/>.
    /// </summary>
    Task<ControlledModel?> FindModelAsync(string modelId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a model, first ejecting any runtime that shares the GPU with it.
    /// The load runs to completion even when the caller stops waiting.
    /// </summary>
    Task<ControlledModel> LoadAsync(string modelId);

    /// <summary>
    /// Ejects the runtime of one kind.
    /// </summary>
    Task EjectAsync(ModelKind kind, CancellationToken cancellationToken);

    /// <summary>
    /// Ejects every runtime.
    /// </summary>
    Task EjectAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asks the app to eject all models and exit.
    /// </summary>
    void RequestShutdown();
}
