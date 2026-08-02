using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IVideoGenerationService
{
    Task<GeneratedVideoResult> GenerateAsync(
        string modelId,
        string prompt,
        VideoGenerationPreset preset,
        CancellationToken cancellationToken);
}
