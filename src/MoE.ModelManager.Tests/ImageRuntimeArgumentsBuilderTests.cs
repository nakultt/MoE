using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ImageRuntimeArgumentsBuilderTests
{
    [TestMethod]
    public void BuildUsesFourStepGpuOptimizedConfiguration()
    {
        ImageModelDefinition model = CreateModel();

        IReadOnlyList<string> arguments = ImageRuntimeArgumentsBuilder.Build(model, 8093);

        CollectionAssert.Contains(arguments.ToList(), "--diffusion-fa");
        CollectionAssert.Contains(arguments.ToList(), "--vae-conv-direct");
        CollectionAssert.Contains(arguments.ToList(), "diffusion=CUDA0,te=CUDA0,vae=CUDA0");
        int stepsIndex = arguments.ToList().IndexOf("--steps");
        Assert.AreEqual("4", arguments[stepsIndex + 1]);
    }

    [TestMethod]
    public void BuildPassesEveryConfiguredModelPathAsSeparateArgument()
    {
        ImageModelDefinition model = CreateModel();

        IReadOnlyList<string> arguments = ImageRuntimeArgumentsBuilder.Build(model, 8093);

        CollectionAssert.Contains(arguments.ToList(), model.DiffusionModelPath);
        CollectionAssert.Contains(arguments.ToList(), model.TextEncoderPath);
        CollectionAssert.Contains(arguments.ToList(), model.VaePath);
    }

    [DataRow(0)]
    [DataRow(1023)]
    [DataRow(65536)]
    [TestMethod]
    public void BuildRejectsInvalidBackendPort(int port)
    {
        ImageModelDefinition model = CreateModel();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ImageRuntimeArgumentsBuilder.Build(model, port));
    }

    private static ImageModelDefinition CreateModel()
    {
        return new ImageModelDefinition
        {
            Id = "flux-test",
            DisplayName = "FLUX test",
            Description = "Test image model",
            DiffusionModelPath = @"C:\Models\flux.safetensors",
            TextEncoderPath = @"C:\Models\qwen.gguf",
            VaePath = @"C:\Models\vae.safetensors",
            Steps = 4,
            Threads = 14,
        };
    }
}
