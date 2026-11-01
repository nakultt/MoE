namespace MoE.ModelManager.Models;

/// <summary>
/// Represents one chat message as written to the local history store.
/// </summary>
/// <remarks>
/// Media bytes are never persisted; only the attachment file name and kind are kept so the
/// history stays small and does not copy private files.
/// </remarks>
public sealed class StoredChatMessage
{
    /// <summary>
    /// Gets or sets the OpenAI chat role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author label shown in the conversation.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the visible message text.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the thinking-channel text captured before the answer.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model identifier for assistant messages.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the attached media file name, if any.
    /// </summary>
    public string? AttachmentFileName { get; set; }

    /// <summary>
    /// Gets or sets the OpenAI content-part type of the attached media, if any.
    /// </summary>
    public string? AttachmentKind { get; set; }

    /// <summary>
    /// Gets or sets the time the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the decode speed of an assistant reply, or 0 when unknown.
    /// </summary>
    public double TokensPerSecond { get; set; }
}
