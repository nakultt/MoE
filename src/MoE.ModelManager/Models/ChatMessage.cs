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
    /// Gets or sets an optional base64 data URL attached to this message.
    /// </summary>
    [ObservableProperty]
    public partial string? ImageDataUrl { get; set; }

    /// <summary>
    /// Gets or sets the display name of the attached image.
    /// </summary>
    [ObservableProperty]
    public partial string? ImageFileName { get; set; }

    /// <summary>
    /// Gets a value indicating whether this message contains assistant Markdown.
    /// </summary>
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.Ordinal);

    /// <summary>
    /// Gets the visible attachment label for a multimodal message.
    /// </summary>
    public string ImageAttachmentSummary => string.IsNullOrWhiteSpace(ImageFileName)
        ? string.Empty
        : $"📎 {ImageFileName}";
}
