using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class ModelCatalogService : IModelCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<ModelDefinition>> LoadAsync(CancellationToken cancellationToken)
    {
        string catalogPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "models.json");
        await using FileStream stream = File.OpenRead(catalogPath);
        List<ModelDefinition>? models = await JsonSerializer.DeserializeAsync<List<ModelDefinition>>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (models is null || models.Count == 0)
        {
            throw new InvalidDataException("The local model catalog is empty.");
        }

        return models;
    }
}
