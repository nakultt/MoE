using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ModelControlRulesTests
{
    [DataRow("text", ModelKind.Text)]
    [DataRow("IMAGE", ModelKind.Image)]
    [DataRow(" video ", ModelKind.Video)]
    [DataRow("speech-to-text", ModelKind.SpeechToText)]
    [DataRow("text-to-speech", ModelKind.TextToSpeech)]
    [TestMethod]
    public void TryParseKind_KnownName_ReturnsKind(string name, ModelKind expected)
    {
        bool parsed = ModelControlRules.TryParseKind(name, out ModelKind kind);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, kind);
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("all")]
    [DataRow("audio")]
    [TestMethod]
    public void TryParseKind_UnknownName_ReturnsFalse(string? name)
    {
        Assert.IsFalse(ModelControlRules.TryParseKind(name, out _));
    }

    [TestMethod]
    public void ToApiName_EveryKind_RoundTripsThroughTryParseKind()
    {
        foreach (ModelKind kind in Enum.GetValues<ModelKind>())
        {
            Assert.IsTrue(ModelControlRules.TryParseKind(ModelControlRules.ToApiName(kind), out ModelKind parsed));
            Assert.AreEqual(kind, parsed);
        }
    }

    [DataRow(ModelKind.Text, ModelKind.Image, ModelKind.Video)]
    [DataRow(ModelKind.Image, ModelKind.Text, ModelKind.Video)]
    [DataRow(ModelKind.Video, ModelKind.Text, ModelKind.Image)]
    [TestMethod]
    public void GetConflictingKinds_GpuKind_ReturnsOtherGpuKinds(ModelKind kind, ModelKind first, ModelKind second)
    {
        CollectionAssert.AreEquivalent(new[] { first, second }, ModelControlRules.GetConflictingKinds(kind).ToArray());
    }

    [DataRow(ModelKind.SpeechToText)]
    [DataRow(ModelKind.TextToSpeech)]
    [TestMethod]
    public void GetConflictingKinds_SpeechKind_ReturnsEmpty(ModelKind kind)
    {
        Assert.AreEqual(0, ModelControlRules.GetConflictingKinds(kind).Count);
    }

    [TestMethod]
    public void IsHeadless_ArgumentList_DetectsSwitchCaseInsensitively()
    {
        Assert.IsTrue(ModelControlRules.IsHeadless(["MoE.ModelManager.exe", "--HEADLESS"]));
        Assert.IsFalse(ModelControlRules.IsHeadless(["MoE.ModelManager.exe", "--headless-ish"]));
        Assert.IsFalse(ModelControlRules.IsHeadless((IEnumerable<string>?)null));
    }

    [DataRow("--headless", true)]
    [DataRow("\"C:\\Apps\\moe-manager.exe\" --headless", true)]
    [DataRow("\"--headless\"", true)]
    [DataRow("", false)]
    [DataRow(null, false)]
    [DataRow("--verbose", false)]
    [TestMethod]
    public void IsHeadless_RawArguments_DetectsSwitch(string? arguments, bool expected)
    {
        Assert.AreEqual(expected, ModelControlRules.IsHeadless(arguments));
    }
}
