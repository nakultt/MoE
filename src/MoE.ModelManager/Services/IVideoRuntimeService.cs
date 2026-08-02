using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IVideoRuntimeService : IAsyncDisposable
{
    event Action<RuntimeSnapshot>? StateChanged;

    RuntimeSnapshot Current { get; }

    Task LoadAsync(VideoModelDefinition model, CancellationToken cancellationToken);

    Task EjectAsync(CancellationToken cancellationToken);
}
