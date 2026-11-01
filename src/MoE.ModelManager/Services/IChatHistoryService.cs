using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Stores chat conversations on this PC.
/// </summary>
internal interface IChatHistoryService
{
    /// <summary>
    /// Gets the folder that holds saved conversations.
    /// </summary>
    string HistoryDirectory { get; }

    /// <summary>
    /// Lists saved conversations, newest first.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The saved conversation summaries.</returns>
    Task<IReadOnlyList<ChatConversationSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads one saved conversation.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The conversation, or <see langword="null"/> when it no longer exists.</returns>
    Task<ChatConversation?> LoadAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or replaces a saved conversation.
    /// </summary>
    /// <param name="conversation">The conversation to save.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the conversation is written.</returns>
    Task SaveAsync(ChatConversation conversation, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one saved conversation.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the conversation is deleted.</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every saved conversation.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the history is empty.</returns>
    Task DeleteAllAsync(CancellationToken cancellationToken);
}
