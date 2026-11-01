using System.ComponentModel;
using System.Globalization;

namespace MoE.ModelManager.Models;

/// <summary>
/// Describes a saved conversation in the history list without loading its messages.
/// </summary>
public sealed class ChatConversationSummary : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _modelName = string.Empty;
    private DateTimeOffset _updatedAt;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the stable conversation identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the conversation title.
    /// </summary>
    public required string Title
    {
        get => _title;
        init => _title = value;
    }

    /// <summary>
    /// Gets the display name of the model used most recently in the conversation.
    /// </summary>
    public string ModelName
    {
        get => _modelName;
        init => _modelName = value;
    }

    /// <summary>
    /// Gets the time the conversation last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        init => _updatedAt = value;
    }

    /// <summary>
    /// Gets a short, culture-aware label for when the conversation last changed.
    /// </summary>
    public string UpdatedText => FormatRelative(UpdatedAt, DateTimeOffset.Now, CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets the secondary caption shown under the conversation title.
    /// </summary>
    public string Caption => string.IsNullOrWhiteSpace(ModelName)
        ? UpdatedText
        : string.Create(CultureInfo.CurrentCulture, $"{UpdatedText} · {ModelName}");

    /// <summary>
    /// Formats a timestamp as a time for today, a weekday within the past week, or a short date.
    /// </summary>
    /// <param name="value">The timestamp to format.</param>
    /// <param name="now">The current time.</param>
    /// <param name="culture">The culture used for formatting.</param>
    /// <returns>The formatted label.</returns>
    public static string FormatRelative(DateTimeOffset value, DateTimeOffset now, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        DateTime local = value.ToLocalTime().DateTime;
        DateTime today = now.ToLocalTime().Date;
        if (local.Date == today)
        {
            return local.ToString("t", culture);
        }

        if (local.Date > today.AddDays(-7))
        {
            return local.ToString("ddd", culture);
        }

        return local.ToString("d", culture);
    }

    /// <summary>
    /// Copies the visible fields of another summary of the same conversation into this one, so
    /// the history list can update an entry without replacing (and redrawing) its row.
    /// </summary>
    /// <param name="source">The newer summary.</param>
    public void UpdateFrom(ChatConversationSummary source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _title = source.Title;
        _modelName = source.ModelName;
        _updatedAt = source.UpdatedAt;
        foreach (string name in (string[])[nameof(Title), nameof(ModelName), nameof(UpdatedAt), nameof(UpdatedText), nameof(Caption)])
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
