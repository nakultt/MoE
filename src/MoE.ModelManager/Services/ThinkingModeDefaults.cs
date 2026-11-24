using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoE.ModelManager.Services;

/// <summary>
/// Applies the app's non-thinking default to OpenAI chat completion requests.
/// </summary>
internal static class ThinkingModeDefaults
{
    /// <summary>
    /// Defaults a chat request to non-thinking unless the caller opted in explicitly.
    /// </summary>
    /// <remarks>
    /// Reasoning-capable templates disagree on their own default: Qwen3.6 and Nemotron
    /// Omni think unless told otherwise, Gemma 4 does not. Every API client therefore
    /// gets the same non-thinking default, and an explicit
    /// <c>chat_template_kwargs.enable_thinking</c> is always honoured. Inkling 2 reads
    /// <c>reasoning_effort</c> instead of <c>enable_thinking</c>, so a non-thinking request
    /// also gets <c>reasoning_effort = "none"</c> unless the caller set an effort. Inkling still
    /// writes a short plan at effort zero; a zero <c>reasoning_budget_tokens</c> must not be
    /// forced on it, because Inkling then loops on empty thinking blocks until the token limit.
    /// </remarks>
    /// <param name="body">The UTF-8 JSON chat completion request.</param>
    /// <returns>The request with the thinking defaults applied.</returns>
    internal static byte[] Apply(ReadOnlySpan<byte> body)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(body);
            if (node is not JsonObject requestObject)
            {
                return body.ToArray();
            }

            if (requestObject["chat_template_kwargs"] is not JsonObject templateArguments)
            {
                templateArguments = [];
                requestObject["chat_template_kwargs"] = templateArguments;
            }

            if (!templateArguments.ContainsKey("enable_thinking"))
            {
                templateArguments["enable_thinking"] = false;
            }

            bool thinking = templateArguments["enable_thinking"] is JsonValue value
                && value.TryGetValue(out bool enabled)
                && enabled;
            if (!thinking
                && !templateArguments.ContainsKey("reasoning_effort")
                && !requestObject.ContainsKey("reasoning_effort"))
            {
                templateArguments["reasoning_effort"] = "none";
            }

            return JsonSerializer.SerializeToUtf8Bytes(requestObject);
        }
        catch (JsonException)
        {
            return body.ToArray();
        }
    }
}
