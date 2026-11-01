using System.Globalization;
using System.Text;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Converts a saved conversation into a portable Markdown transcript.
/// </summary>
internal static class ChatTranscriptFormatter
{
    /// <summary>
    /// Formats a conversation as Markdown.
    /// </summary>
    /// <param name="conversation">The conversation to export.</param>
    /// <param name="culture">The culture used for dates.</param>
    /// <returns>The Markdown transcript.</returns>
    public static string ToMarkdown(ChatConversation conversation, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(culture);
        StringBuilder builder = new();
        string title = string.IsNullOrWhiteSpace(conversation.Title) ? "Chat" : conversation.Title.Trim();
        builder.Append("# ").AppendLine(title).AppendLine();
        builder.Append(culture, $"_{conversation.CreatedAt.ToLocalTime().ToString("f", culture)}");
        if (!string.IsNullOrWhiteSpace(conversation.ModelName))
        {
            builder.Append(" · ").Append(conversation.ModelName);
        }

        builder.AppendLine("_").AppendLine();
        foreach (StoredChatMessage message in conversation.Messages)
        {
            string author = string.IsNullOrWhiteSpace(message.Author) ? message.Role : message.Author;
            builder.Append("## ").AppendLine(author).AppendLine();
            if (!string.IsNullOrWhiteSpace(message.AttachmentFileName))
            {
                builder.Append("> Attachment: ").AppendLine(message.AttachmentFileName).AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(message.Reasoning))
            {
                builder.AppendLine("<details><summary>Thinking</summary>").AppendLine();
                builder.AppendLine(message.Reasoning.Trim()).AppendLine();
                builder.AppendLine("</details>").AppendLine();
            }

            builder.AppendLine(message.Content.Trim()).AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>
    /// Creates a short conversation title from the first user prompt.
    /// </summary>
    /// <param name="prompt">The first prompt in the conversation.</param>
    /// <param name="maximumLength">The longest title to return.</param>
    /// <returns>A single-line title.</returns>
    public static string CreateTitle(string prompt, int maximumLength = 60)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        string singleLine = string.Join(' ', prompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (singleLine.Length <= maximumLength)
        {
            return singleLine;
        }

        int cut = singleLine.LastIndexOf(' ', maximumLength - 1);
        if (cut < maximumLength / 2)
        {
            cut = maximumLength - 1;
        }

        return string.Concat(singleLine.AsSpan(0, cut).TrimEnd(), "…");
    }

    /// <summary>
    /// Creates a safe file name for an exported transcript.
    /// </summary>
    /// <param name="title">The conversation title.</param>
    /// <returns>A file name without an extension.</returns>
    public static string CreateFileName(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(title.Select(character => invalid.Contains(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' ? ' ' : character).ToArray());
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim().TrimEnd('.', '…');
        if (cleaned.Length > 80)
        {
            cleaned = cleaned[..80].TrimEnd();
        }

        return cleaned.Length == 0 ? "Chat" : cleaned;
    }
}
