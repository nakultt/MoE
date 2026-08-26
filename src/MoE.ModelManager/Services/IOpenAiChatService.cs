using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IOpenAiChatService
{
    IAsyncEnumerable<ChatStreamDelta> StreamResponseAsync(
        string modelId,
        IReadOnlyList<ChatMessage> messages,
        bool? enableThinking,
        int thinkingBudgetTokens,
        CancellationToken cancellationToken);
}
