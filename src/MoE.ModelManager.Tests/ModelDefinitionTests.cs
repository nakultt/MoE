using MoE.ModelManager.Models;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ModelDefinitionTests
{
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("mmproj.gguf", true)]
    [TestMethod]
    public void HasVisionRequiresProjectorPath(string? path, bool expected)
    {
        ModelDefinition model = CreateModel(visionProjectorPath: path);

        Assert.AreEqual(expected, model.HasVision);
    }

    [DataRow(null, 2, false)]
    [DataRow("", 2, false)]
    [DataRow("draft.gguf", 0, false)]
    [DataRow("draft.gguf", 2, true)]
    [TestMethod]
    public void HasDraftModelRequiresPathAndPositiveTokenCount(string? path, int draftTokens, bool expected)
    {
        ModelDefinition model = CreateModel(draftModelPath: path, draftTokens: draftTokens);

        Assert.AreEqual(expected, model.HasDraftModel);
    }

    [TestMethod]
    public void HasMultimodalInputIncludesAudioAndVideoCapabilities()
    {
        ModelDefinition audioModel = CreateModel(supportsAudio: true);
        ModelDefinition videoModel = CreateModel(supportsVideo: true);

        Assert.IsTrue(audioModel.HasMultimodalInput);
        Assert.IsTrue(videoModel.HasMultimodalInput);
    }

    private static ModelDefinition CreateModel(
        string? visionProjectorPath = null,
        string? draftModelPath = null,
        int draftTokens = 0,
        bool supportsAudio = false,
        bool supportsVideo = false)
    {
        return new ModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test model",
            Description = "Test description",
            ModelPath = "model.gguf",
            VisionProjectorPath = visionProjectorPath,
            SupportsAudio = supportsAudio,
            SupportsVideo = supportsVideo,
            ContextSize = 8192,
            CpuMoeLayers = 20,
            CacheType = "q8_0",
            Threads = 16,
            DraftModelPath = draftModelPath,
            DraftTokens = draftTokens,
            MeasuredTokensPerSecond = 50.65,
        };
    }
}
