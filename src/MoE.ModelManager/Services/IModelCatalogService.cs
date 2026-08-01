using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IModelCatalogService
{
    Task<IReadOnlyList<ModelDefinition>> LoadAsync(CancellationToken cancellationToken);
}
