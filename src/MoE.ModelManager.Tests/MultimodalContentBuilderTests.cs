using System.Text.Json.Nodes;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class MultimodalContentBuilderTests
{
    [TestMethod]
    public void AudioIsPlacedBeforeItsTextPrompt()
    {
        JsonArray content = MultimodalContentBuilder.Build(
            "Transcribe this audio.",
            "input_audio",
            "data:audio/wav;base64,UklGRg==");

        Assert.AreEqual("input_audio", content[0]!["type"]!.GetValue<string>());
        Assert.AreEqual("UklGRg==", content[0]!["input_audio"]!["data"]!.GetValue<string>());
        Assert.AreEqual("text", content[1]!["type"]!.GetValue<string>());
        Assert.AreEqual("Transcribe this audio.", content[1]!["text"]!.GetValue<string>());
    }

    [TestMethod]
    public void VideoAndCompanionAudioArePlacedBeforeText()
    {
        JsonArray content = MultimodalContentBuilder.Build(
            "Describe what you see and hear.",
            "input_video",
            "data:video/mp4;base64,AAAA",
            "data:audio/wav;base64,UklG");

        CollectionAssert.AreEqual(
            new[] { "input_video", "input_audio", "text" },
            content.Select(part => part!["type"]!.GetValue<string>()).ToArray());
    }

    [TestMethod]
    public void ImageRetainsTextFirstReferenceOrdering()
    {
        JsonArray content = MultimodalContentBuilder.Build(
            "Describe this image.",
            "image_url",
            "data:image/png;base64,iVBORw==");

        CollectionAssert.AreEqual(
            new[] { "text", "image_url" },
            content.Select(part => part!["type"]!.GetValue<string>()).ToArray());
    }
}
