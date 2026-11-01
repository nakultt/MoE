using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ModelBrandCatalogTests
{
    [DataRow("qwen3.6-35b-a3b", "qwen.png", "Alibaba Cloud")]
    [DataRow("gemma-4-26b-a4b", "gemma.png", "Google")]
    [DataRow("nemotron-3-nano-omni-30b-a3b", "nvidia.png", "NVIDIA")]
    [DataRow("parakeet-v3-kokoro-82m", "nvidia.png", "NVIDIA")]
    [DataRow("kokoro", "kokoro.png", "hexgrad")]
    [DataRow("flux.2-klein-4b", "flux.png", "Black Forest Labs")]
    [DataRow("fastwan2.2-ti2v-5b", "wan.png", "Wan")]
    [DataRow("FLUX.2 Klein 4B Distilled", "flux.png", "Black Forest Labs")]
    [TestMethod]
    public void GetLogo_KnownModel_ReturnsPublisherLogo(string modelKey, string asset, string publisher)
    {
        Uri logo = ModelBrandCatalog.GetLogo(modelKey);

        Assert.AreEqual($"ms-appx:///Assets/Brands/{asset}", logo.OriginalString);
        Assert.AreEqual(publisher, ModelBrandCatalog.GetPublisher(modelKey));
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("mystery-model-7b")]
    [TestMethod]
    public void GetLogo_UnknownModel_ReturnsAppLogo(string? modelKey)
    {
        Assert.AreEqual(ModelBrandCatalog.FallbackLogo, ModelBrandCatalog.GetLogo(modelKey));
        Assert.AreEqual(string.Empty, ModelBrandCatalog.GetPublisher(modelKey));
    }
}
