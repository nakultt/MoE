using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IVideoModelCatalogService
{
    Task<VideoModelDefinition> LoadAsync(CancellationToken cancellationToken);
}
