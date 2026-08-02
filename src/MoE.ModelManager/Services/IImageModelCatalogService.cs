using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IImageModelCatalogService
{
    Task<ImageModelDefinition> LoadAsync(CancellationToken cancellationToken);
}
