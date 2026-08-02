using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class VideoRuntimeConfigurationBuilderTests
{
    [TestMethod]
    public void BuildUsesLayerwiseFp8ConfigurationAndThreeStepDefaults()
    {
        VideoModelDefinition model = CreateModel("C:\\Models\\FastWan");

        string result = VideoRuntimeConfigurationBuilder.Build(model, "C:\\Output Videos", 8094);

        StringAssert.Contains(result, "model_path: '/mnt/c/Models/FastWan'");
        StringAssert.Contains(result, "dit_layerwise: true");
        StringAssert.Contains(result, "pin_cpu_memory: false");
        StringAssert.Contains(result, "transformer_quant: FP8");
        StringAssert.Contains(result, "dmd_denoising_steps: [1000, 757, 522]");
        StringAssert.Contains(result, "num_frames: 49");
        StringAssert.Contains(result, "output_dir: '/mnt/c/Output Videos'");
        StringAssert.Contains(result, "output_path: '/mnt/c/Output Videos'");
        StringAssert.Contains(result, "port: 8094");
    }

    [TestMethod]
    public void ToWslPathMapsDriveAndPreservesSpaces()
    {
        string result = VideoRuntimeConfigurationBuilder.ToWslPath("E:\\Github\\MoE Model");

        Assert.AreEqual("/mnt/e/Github/MoE Model", result);
    }

    [TestMethod]
    public void BuildRejectsPrivilegedPort()
    {
        VideoModelDefinition model = CreateModel("C:\\Models\\FastWan");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => VideoRuntimeConfigurationBuilder.Build(model, "C:\\Output", 80));
    }

    private static VideoModelDefinition CreateModel(string path)
    {
        return new VideoModelDefinition
        {
            Id = "fastwan",
            DisplayName = "FastWan",
            Description = "Test model",
            ModelPath = path,
        };
    }
}
