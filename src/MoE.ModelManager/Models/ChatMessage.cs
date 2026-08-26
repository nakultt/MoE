using CommunityToolkit.Mvvm.ComponentModel;

namespace MoE.ModelManager.Models;

/// <summary>
/// Represents one visible chat message.
/// </summary>
public sealed partial class ChatMessage : ObservableObject
{
    /// <summary>
    /// Gets or sets the OpenAI chat role.
    /// </summary>
    [ObservableProperty]
    public partial string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the localized author label.
    /// </summary>
    [ObservableProperty]
    public partial string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the thinking-channel text streamed before the answer.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReasoning))]
    public partial string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional base64 data URL attached to this message.
    /// </summary>
    [ObservableProperty]
    public partial string? AttachmentDataUrl { get; set; }

    [ObservableProperty]
    public partial string? CompanionAudioDataUrl { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string>? VideoFrameDataUrls { get; set; }

    /// <summary>
    /// Gets or sets the display name of the attached media file.
    /// </summary>
    [ObservableProperty]
    public partial string? AttachmentFileName { get; set; }

    /// <summary>
    /// Gets or sets the OpenAI content-part type for the attached media.
    /// </summary>
    [ObservableProperty]
    public partial string? AttachmentKind { get; set; }

    /// <summary>
    /// Gets a value indicating whether this message contains assistant Markdown.
    /// </summary>
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.Ordinal);

    /// <summary>
    /// Gets a value indicating whether a thinking trace was captured for this message.
    /// </summary>
    public bool HasReasoning => !string.IsNullOrWhiteSpace(Reasoning);

    /// <summary>
    /// Gets the visible attachment label for a multimodal message.
    /// </summary>
    public string AttachmentSummary => string.IsNullOrWhiteSpace(AttachmentFileName)
        ? string.Empty
        : $"📎 {AttachmentFileName}";
}
