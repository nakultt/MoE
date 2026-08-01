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
    /// Gets a value indicating whether this message contains assistant Markdown.
    /// </summary>
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.Ordinal);
}
