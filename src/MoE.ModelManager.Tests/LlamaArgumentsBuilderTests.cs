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
    public void BuildOmitsMtpArgumentsWithoutDraftModel()
    {
        ModelDefinition model = CreateModel(draftModelPath: null, draftTokens: 0);

        IReadOnlyList<string> arguments = LlamaArgumentsBuilder.Build(model, 8092);

        CollectionAssert.DoesNotContain(arguments.ToList(), "--spec-type");
        CollectionAssert.Contains(arguments.ToList(), "-rea");
        CollectionAssert.Contains(arguments.ToList(), "off");
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

    private static ModelDefinition CreateModel(string? draftModelPath, int draftTokens)
    {
        return new ModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test model",
            Description = "Test description",
            ModelPath = @"C:\Models\model.gguf",
            ContextSize = 8192,
            CpuMoeLayers = 20,
            DraftModelPath = draftModelPath,
            DraftTokens = draftTokens,
            MeasuredTokensPerSecond = 50.65,
        };
    }
}
