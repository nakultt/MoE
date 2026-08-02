using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class VideoModelCatalogService : IVideoModelCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<VideoModelDefinition> LoadAsync(CancellationToken cancellationToken)
    {
        string catalogPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "video-model.json");
        await using FileStream stream = File.OpenRead(catalogPath);
        VideoModelDefinition? model = await JsonSerializer.DeserializeAsync<VideoModelDefinition>(
            stream,
            SerializerOptions,
            cancellationToken);
        return model ?? throw new InvalidDataException("The video model configuration is empty.");
    }
}
