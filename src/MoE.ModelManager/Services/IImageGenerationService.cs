using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IImageGenerationService
{
    Task<GeneratedImageResult> GenerateAsync(
        string modelId,
        string prompt,
        string size,
        CancellationToken cancellationToken);
}
