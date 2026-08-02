using System.Globalization;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal static class LlamaArgumentsBuilder
{
    internal static IReadOnlyList<string> Build(ModelDefinition model, int backendPort)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (backendPort is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(backendPort));
        }

        string context = model.ContextSize.ToString(CultureInfo.InvariantCulture);
        string cpuMoeLayers = model.CpuMoeLayers.ToString(CultureInfo.InvariantCulture);
        string threads = model.Threads.ToString(CultureInfo.InvariantCulture);
        string port = backendPort.ToString(CultureInfo.InvariantCulture);
        List<string> arguments =
        [
            "-m", model.ModelPath,
            "-c", context,
            "-ngl", "999",
            "-ncmoe", cpuMoeLayers,
            "-ctk", model.CacheType,
            "-ctv", model.CacheType,
            "-fa", "on",
            "-t", threads,
            "-C", "0xFFFF",
            "--cpu-strict", "1",
            "--prio", "2",
            "-np", "1",
            "--host", "127.0.0.1",
            "--port", port,
            "--alias", model.Id,
            "--jinja",
            "-rea", "off",
            "--metrics",
        ];

        if (model.HasDraftModel)
        {
            arguments.AddRange(
            [
                "--spec-type", "draft-mtp",
                "--spec-draft-model", model.DraftModelPath!,
                "--spec-draft-n-max", model.DraftTokens.ToString(CultureInfo.InvariantCulture),
                "--spec-draft-ngl", "999",
            ]);
        }

        return arguments;
    }
}
