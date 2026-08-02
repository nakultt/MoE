using System.Globalization;
using System.Text;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal static class VideoRuntimeConfigurationBuilder
{
    internal static string Build(
        VideoModelDefinition model,
        string outputDirectory,
        int port)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        string modelPath = ToWslPath(model.ModelPath);
        string outputPath = ToWslPath(outputDirectory);
        StringBuilder yaml = new();
        yaml.AppendLine("generator:");
        yaml.AppendLine(CultureInfo.InvariantCulture, $"  model_path: {QuoteYaml(modelPath)}");
        yaml.AppendLine("  engine:");
        yaml.AppendLine("    num_gpus: 1");
        yaml.AppendLine("    parallelism:");
        yaml.AppendLine("      tp_size: 1");
        yaml.AppendLine("      sp_size: 1");
        yaml.AppendLine("    offload:");
        yaml.AppendLine("      dit: false");
        yaml.AppendLine("      dit_layerwise: true");
        yaml.AppendLine("      text_encoder: true");
        yaml.AppendLine("      image_encoder: true");
        yaml.AppendLine("      vae: true");
        yaml.AppendLine("      pin_cpu_memory: false");
        yaml.AppendLine("    compile:");
        yaml.AppendLine("      enabled: false");
        yaml.AppendLine("    quantization:");
        yaml.AppendLine("      transformer_quant: FP8");
        yaml.AppendLine("  pipeline:");
        yaml.AppendLine("    workload_type: t2v");
        yaml.AppendLine("    experimental:");
        yaml.AppendLine("      dmd_denoising_steps: [1000, 757, 522]");
        yaml.AppendLine("server:");
        yaml.AppendLine("  host: 127.0.0.1");
        yaml.AppendLine(CultureInfo.InvariantCulture, $"  port: {port}");
        yaml.AppendLine(CultureInfo.InvariantCulture, $"  output_dir: {QuoteYaml(outputPath)}");
        yaml.AppendLine("default_request:");
        yaml.AppendLine("  sampling:");
        yaml.AppendLine("    height: 448");
        yaml.AppendLine("    width: 448");
        yaml.AppendLine("    num_frames: 49");
        yaml.AppendLine("    fps: 16");
        yaml.AppendLine("    num_inference_steps: 3");
        yaml.AppendLine("    guidance_scale: 1.0");
        yaml.AppendLine("  output:");
        yaml.AppendLine(CultureInfo.InvariantCulture, $"    output_path: {QuoteYaml(outputPath)}");
        yaml.AppendLine("    save_video: true");
        yaml.AppendLine("    return_frames: false");
        return yaml.ToString();
    }

    internal static string ToWslPath(string windowsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsPath);
        string fullPath = Path.GetFullPath(windowsPath);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root) || root.Length < 2 || root[1] != ':')
        {
            throw new ArgumentException("Only drive-letter Windows paths can be mapped into WSL.", nameof(windowsPath));
        }

        char drive = char.ToLowerInvariant(root[0]);
        string remainder = fullPath[root.Length..].Replace('\\', '/');
        return $"/mnt/{drive}/{remainder}";
    }

    private static string QuoteYaml(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
