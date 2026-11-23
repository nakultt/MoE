using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IImageModelCatalogService
{
    /// <summary>
    /// Loads the image models; the first entry is the default.
    /// </summary>
    Task<IReadOnlyList<ImageModelDefinition>> LoadAsync(CancellationToken cancellationToken);
}
