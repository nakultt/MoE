using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class ImageModelCatalogService : IImageModelCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ImageModelDefinition> LoadAsync(CancellationToken cancellationToken)
    {
        string catalogPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "image-model.json");
        await using FileStream stream = File.OpenRead(catalogPath);
        ImageModelDefinition? model = await JsonSerializer.DeserializeAsync<ImageModelDefinition>(
            stream,
            SerializerOptions,
            cancellationToken);
        return model ?? throw new InvalidDataException("The image model configuration is empty.");
    }
}
