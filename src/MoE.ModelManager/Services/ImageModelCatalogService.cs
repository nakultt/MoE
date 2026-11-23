using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class ImageModelCatalogService : IImageModelCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<ImageModelDefinition>> LoadAsync(CancellationToken cancellationToken)
    {
        string catalogPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "image-models.json");
        await using FileStream stream = File.OpenRead(catalogPath);
        List<ImageModelDefinition>? models = await JsonSerializer.DeserializeAsync<List<ImageModelDefinition>>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (models is null || models.Count == 0)
        {
            throw new InvalidDataException("The image model catalog is empty.");
        }

        return models;
    }
}
