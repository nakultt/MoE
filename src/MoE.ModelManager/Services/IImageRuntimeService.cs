using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IImageRuntimeService : IAsyncDisposable
{
    event Action<RuntimeSnapshot>? StateChanged;

    RuntimeSnapshot Current { get; }

    Task LoadAsync(ImageModelDefinition model, CancellationToken cancellationToken);

    Task EjectAsync(CancellationToken cancellationToken);
}
