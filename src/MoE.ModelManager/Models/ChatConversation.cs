namespace MoE.ModelManager.Models;

/// <summary>
/// Represents one saved chat conversation.
/// </summary>
public sealed class ChatConversation
{
    /// <summary>
    /// Gets the stable conversation identifier (32 lowercase hexadecimal characters).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the conversation title shown in the history list.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the model that produced the latest reply.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the model that produced the latest reply.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the time the conversation was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets the time the conversation last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the saved messages in conversation order.
    /// </summary>
    public IList<StoredChatMessage> Messages { get; set; } = [];
}
