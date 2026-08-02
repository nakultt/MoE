using System.Globalization;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal static class ImageRuntimeArgumentsBuilder
{
    internal static IReadOnlyList<string> Build(ImageModelDefinition model, int port)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        return
        [
            "--listen-ip", "127.0.0.1",
            "--listen-port", port.ToString(CultureInfo.InvariantCulture),
            "--diffusion-model", model.DiffusionModelPath,
            "--llm", model.TextEncoderPath,
            "--vae", model.VaePath,
            "--backend", "diffusion=CUDA0,te=CUDA0,vae=CUDA0",
            "--params-backend", "diffusion=CUDA0,te=CUDA0,vae=CUDA0",
            "--threads", model.Threads.ToString(CultureInfo.InvariantCulture),
            "--steps", model.Steps.ToString(CultureInfo.InvariantCulture),
            "--cfg-scale", "1.0",
            "--seed", "-1",
            "--diffusion-fa",
            "--vae-conv-direct",
            "--mmap",
        ];
    }
}
