using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IAudioModelCatalogService
{
    Task<AudioModelDefinition> LoadAsync(CancellationToken cancellationToken);
}
