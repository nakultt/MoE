using MoE.ModelManager.Models;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ModelDefinitionTests
{
    [DataRow(null, 2, false)]
    [DataRow("", 2, false)]
    [DataRow("draft.gguf", 0, false)]
    [DataRow("draft.gguf", 2, true)]
    [TestMethod]
    public void HasDraftModelRequiresPathAndPositiveTokenCount(string? path, int draftTokens, bool expected)
    {
        ModelDefinition model = new()
        {
            Id = "test-model",
            DisplayName = "Test model",
            Description = "Test description",
            ModelPath = "model.gguf",
            ContextSize = 8192,
            CpuMoeLayers = 20,
            CacheType = "q8_0",
            Threads = 16,
            DraftModelPath = path,
            DraftTokens = draftTokens,
            MeasuredTokensPerSecond = 50.65,
        };

        Assert.AreEqual(expected, model.HasDraftModel);
    }
}
