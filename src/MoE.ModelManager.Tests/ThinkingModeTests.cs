using System.Text;
using System.Text.Json.Nodes;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ThinkingModeTests
{
    [TestMethod]
    public void ApplyDefaultThinkingMode_NoKwargs_DisablesThinkingAndReasoningEffort()
    {
        JsonObject result = Apply("""{"model":"m","messages":[]}""");

        Assert.IsFalse(result["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
        Assert.AreEqual("none", result["chat_template_kwargs"]!["reasoning_effort"]!.GetValue<string>());
        Assert.AreEqual(0, result["reasoning_budget_tokens"]!.GetValue<int>());
    }

    [TestMethod]
    public void ApplyDefaultThinkingMode_ExplicitBudget_IsKept()
    {
        JsonObject result = Apply("""{"model":"m","reasoning_budget_tokens":64}""");

        Assert.AreEqual(64, result["reasoning_budget_tokens"]!.GetValue<int>());
    }

    [TestMethod]
    public void ApplyDefaultThinkingMode_ThinkingEnabled_LeavesReasoningEffortToTemplate()
    {
        JsonObject result = Apply("""{"model":"m","chat_template_kwargs":{"enable_thinking":true}}""");

        Assert.IsTrue(result["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
        Assert.IsNull(result["chat_template_kwargs"]!["reasoning_effort"]);
        Assert.IsNull(result["reasoning_budget_tokens"]);
    }

    [TestMethod]
    public void ApplyDefaultThinkingMode_ExplicitReasoningEffort_IsKept()
    {
        JsonObject result = Apply("""{"model":"m","chat_template_kwargs":{"enable_thinking":false,"reasoning_effort":"low"}}""");

        Assert.AreEqual("low", result["chat_template_kwargs"]!["reasoning_effort"]!.GetValue<string>());
    }

    [TestMethod]
    public void ApplyDefaultThinkingMode_TopLevelReasoningEffort_IsNotOverridden()
    {
        JsonObject result = Apply("""{"model":"m","reasoning_effort":"high"}""");

        Assert.IsNull(result["chat_template_kwargs"]!["reasoning_effort"]);
        Assert.AreEqual("high", result["reasoning_effort"]!.GetValue<string>());
    }

    private static JsonObject Apply(string json)
    {
        byte[] output = ThinkingModeDefaults.Apply(Encoding.UTF8.GetBytes(json));
        return JsonNode.Parse(output)!.AsObject();
    }
}
