using System.Globalization;

namespace MoE.ModelManager.Models;

/// <summary>
/// Describes a saved conversation in the history list without loading its messages.
/// </summary>
public sealed class ChatConversationSummary
{
    /// <summary>
    /// Gets the stable conversation identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the conversation title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the display name of the model used most recently in the conversation.
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the time the conversation last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

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
}
