using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IOpenAiChatService
{
    IAsyncEnumerable<string> StreamResponseAsync(
        string modelId,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);
}
