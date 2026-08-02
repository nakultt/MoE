using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class LlamaArgumentsBuilderTests
{
    [TestMethod]
    public void BuildAddsMtpArgumentsWhenDraftModelIsConfigured()
    {
        ModelDefinition model = CreateModel(draftModelPath: @"C:\Models\draft.gguf", draftTokens: 2);

        IReadOnlyList<string> arguments = LlamaArgumentsBuilder.Build(model, 8092);

        CollectionAssert.Contains(arguments.ToList(), "--spec-type");
        CollectionAssert.Contains(arguments.ToList(), "draft-mtp");
        CollectionAssert.Contains(arguments.ToList(), @"C:\Models\draft.gguf");
        CollectionAssert.Contains(arguments.ToList(), "2");
    }

    [TestMethod]
    public void BuildUsesModelSpecificCacheAndThreadSettings()
    {
        ModelDefinition model = CreateModel(draftModelPath: null, draftTokens: 0);

        IReadOnlyList<string> arguments = LlamaArgumentsBuilder.Build(model, 8092);

        int cacheKeyIndex = arguments.ToList().IndexOf("-ctk");
        int cacheValueIndex = arguments.ToList().IndexOf("-ctv");
        int threadsIndex = arguments.ToList().IndexOf("-t");
        Assert.AreEqual("f16", arguments[cacheKeyIndex + 1]);
        Assert.AreEqual("f16", arguments[cacheValueIndex + 1]);
        Assert.AreEqual("14", arguments[threadsIndex + 1]);
    }

    [TestMethod]
    public void BuildOmitsMtpArgumentsWithoutDraftModel()
    {
        ModelDefinition model = CreateModel(draftModelPath: null, draftTokens: 0);

        IReadOnlyList<string> arguments = LlamaArgumentsBuilder.Build(model, 8092);

        CollectionAssert.DoesNotContain(arguments.ToList(), "--spec-type");
        CollectionAssert.Contains(arguments.ToList(), "-rea");
        CollectionAssert.Contains(arguments.ToList(), "off");
    }

    [TestMethod]
    public void BuildAddsCpuResidentVisionProjectorWhenConfigured()
    {
        ModelDefinition model = CreateModel(
            draftModelPath: null,
            draftTokens: 0,
            visionProjectorPath: @"C:\Models\mmproj.gguf",
            visionImageTokens: 1024);

        IReadOnlyList<string> arguments = LlamaArgumentsBuilder.Build(model, 8092);

        CollectionAssert.Contains(arguments.ToList(), "--mmproj");
        CollectionAssert.Contains(arguments.ToList(), @"C:\Models\mmproj.gguf");
        CollectionAssert.Contains(arguments.ToList(), "--no-mmproj-offload");
        CollectionAssert.Contains(arguments.ToList(), "--image-min-tokens");
        CollectionAssert.Contains(arguments.ToList(), "--image-max-tokens");
        CollectionAssert.Contains(arguments.ToList(), "1024");
    }

    [DataRow(0)]
    [DataRow(1023)]
    [DataRow(65536)]
    [TestMethod]
    public void BuildRejectsInvalidBackendPort(int port)
    {
        ModelDefinition model = CreateModel(draftModelPath: null, draftTokens: 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LlamaArgumentsBuilder.Build(model, port));
    }

    private static ModelDefinition CreateModel(
        string? draftModelPath,
        int draftTokens,
        string? visionProjectorPath = null,
        int visionImageTokens = 0)
    {
        return new ModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test model",
            Description = "Test description",
            ModelPath = @"C:\Models\model.gguf",
            VisionProjectorPath = visionProjectorPath,
            VisionImageTokens = visionImageTokens,
            ContextSize = 8192,
            CpuMoeLayers = 20,
            CacheType = "f16",
            Threads = 14,
            DraftModelPath = draftModelPath,
            DraftTokens = draftTokens,
            MeasuredTokensPerSecond = 50.65,
        };
    }
}
