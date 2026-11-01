using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Helpers;

/// <summary>
/// Chooses the right-aligned bubble for the user's messages and the avatar layout for model replies.
/// </summary>
public sealed partial class ChatMessageTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Gets or sets the template used for the user's messages.
    /// </summary>
    public DataTemplate? UserTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template used for model replies.
    /// </summary>
    public DataTemplate? AssistantTemplate { get; set; }

    /// <inheritdoc />
    protected override DataTemplate SelectTemplateCore(object item)
    {
        DataTemplate? template = item is ChatMessage { IsAssistant: true } ? AssistantTemplate : UserTemplate;
        return template ?? base.SelectTemplateCore(item);
    }

    /// <inheritdoc />
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
