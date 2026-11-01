namespace MoE.ModelManager.Models;

/// <summary>
/// Represents one streamed chunk of an assistant response.
/// </summary>
/// <param name="Content">The visible answer text, if this chunk carried any.</param>
/// <param name="Reasoning">The thinking-channel text, if this chunk carried any.</param>
/// <param name="PromptTokens">The prompt token count reported by the final usage chunk.</param>
/// <param name="CompletionTokens">The completion token count reported by the final usage chunk.</param>
/// <param name="TokensPerSecond">The decode speed reported by the runtime's final timings, if any.</param>
public readonly record struct ChatStreamDelta(
    string? Content,
    string? Reasoning,
    int PromptTokens,
    int CompletionTokens,
    double TokensPerSecond = 0)
{
    /// <summary>
    /// Gets a value indicating whether this chunk reported context usage.
    /// </summary>
    public bool HasUsage => PromptTokens > 0 || CompletionTokens > 0;
}
