using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MoE.ModelManager.Services;

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
    [NotifyPropertyChangedFor(nameof(HasAttachment))]
    [NotifyPropertyChangedFor(nameof(AttachmentSummary))]
    public partial string? AttachmentFileName { get; set; }

    /// <summary>
    /// Gets or sets the OpenAI content-part type for the attached media.
    /// </summary>
    [ObservableProperty]
    public partial string? AttachmentKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the model that wrote an assistant message.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogoUri))]
    public partial string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time the message was created.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText))]
    public partial DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Gets or sets the decode speed of an assistant reply, or 0 when unknown.
    /// </summary>
    [ObservableProperty]
    public partial double TokensPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the localized decode speed shown beside an assistant reply.
    /// </summary>
    [ObservableProperty]
    public partial string SpeedText { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this message contains assistant Markdown.
    /// </summary>
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.Ordinal);

    /// <summary>
    /// Gets the publisher logo of the model that wrote this message.
    /// </summary>
    public Uri LogoUri => ModelBrandCatalog.GetLogo(ModelId);

    /// <summary>
    /// Gets the culture-aware time the message was created.
    /// </summary>
    public string TimeText => Timestamp.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets a value indicating whether a media file was attached to this message.
    /// </summary>
    public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentFileName);

    /// <summary>
    /// Gets a value indicating whether a thinking trace was captured for this message.
    /// </summary>
    public bool HasReasoning => !string.IsNullOrWhiteSpace(Reasoning);

    /// <summary>
    /// Gets the visible attachment label for a multimodal message.
    /// </summary>
    public string AttachmentSummary => AttachmentFileName ?? string.Empty;
}
