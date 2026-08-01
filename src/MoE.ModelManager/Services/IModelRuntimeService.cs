using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IModelRuntimeService : IAsyncDisposable
{
    event Action<RuntimeSnapshot>? StateChanged;

    RuntimeSnapshot Current { get; }

    Task LoadAsync(ModelDefinition model, CancellationToken cancellationToken);

    Task EjectAsync(CancellationToken cancellationToken);
}
