using Microsoft.UI.Xaml;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Helpers;

/// <summary>
/// Small pure functions used by compiled <c>x:Bind</c> expressions in XAML.
/// </summary>
public static class ViewHelpers
{
    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> when the value is <see langword="true"/>.
    /// </summary>
    /// <param name="value">The condition.</param>
    /// <returns>The matching visibility.</returns>
    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Returns <see cref="Visibility.Collapsed"/> when the value is <see langword="true"/>.
    /// </summary>
    /// <param name="value">The condition.</param>
    /// <returns>The matching visibility.</returns>
    public static Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> when the text is not empty.
    /// </summary>
    /// <param name="value">The text to test.</param>
    /// <returns>The matching visibility.</returns>
    public static Visibility VisibleIfText(string? value) => VisibleIf(HasText(value));

    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> when the value is not <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns>The matching visibility.</returns>
    public static Visibility VisibleIfNotNull(object? value) => VisibleIf(value is not null);

    /// <summary>
    /// Determines whether the text has visible content.
    /// </summary>
    /// <param name="value">The text to test.</param>
    /// <returns><see langword="true"/> when the text is not empty or whitespace.</returns>
    public static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Negates a Boolean value.
    /// </summary>
    /// <param name="value">The value to negate.</param>
    /// <returns>The negated value.</returns>
    public static bool Not(bool value) => !value;

    /// <summary>
    /// Shows a Load button while a runtime is stopped or failed.
    /// </summary>
    /// <param name="status">The runtime status.</param>
    /// <returns>The matching visibility.</returns>
    public static Visibility LoadVisibility(RuntimeStatus status) =>
        VisibleIf(status is RuntimeStatus.Unloaded or RuntimeStatus.Error);

    /// <summary>
    /// Shows an Eject button while a runtime is starting, running, or stopping.
    /// </summary>
    /// <param name="status">The runtime status.</param>
    /// <returns>The matching visibility.</returns>
    public static Visibility EjectVisibility(RuntimeStatus status) =>
        VisibleIf(status is RuntimeStatus.Loading or RuntimeStatus.Ready or RuntimeStatus.Ejecting);

    /// <summary>
    /// Joins a category label and a publisher name.
    /// </summary>
    /// <param name="kind">The category label.</param>
    /// <param name="publisher">The publisher name, which may be empty.</param>
    /// <returns>The combined caption.</returns>
    public static string Caption(string? kind, string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return kind ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(kind) ? publisher : $"{kind} · {publisher}";
    }
}
