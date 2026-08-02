using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class AudioModelCatalogService : IAudioModelCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<AudioModelDefinition> LoadAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Configuration", "audio-model.json");
        await using FileStream stream = File.OpenRead(path);
        AudioModelDefinition? model = await JsonSerializer.DeserializeAsync<AudioModelDefinition>(
            stream,
            SerializerOptions,
            cancellationToken);
        return model ?? throw new InvalidDataException("The audio model configuration is empty.");
    }
}
