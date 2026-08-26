namespace MoE.ModelManager.Models;

/// <summary>
/// Represents one streamed chunk of an assistant response.
/// </summary>
/// <param name="Content">The visible answer text, if this chunk carried any.</param>
/// <param name="Reasoning">The thinking-channel text, if this chunk carried any.</param>
/// <param name="PromptTokens">The prompt token count reported by the final usage chunk.</param>
/// <param name="CompletionTokens">The completion token count reported by the final usage chunk.</param>
public readonly record struct ChatStreamDelta(
    string? Content,
    string? Reasoning,
    int PromptTokens,
    int CompletionTokens)
{
    /// <summary>
    /// Gets a value indicating whether this chunk reported context usage.
    /// </summary>
    public bool HasUsage => PromptTokens > 0 || CompletionTokens > 0;
}
