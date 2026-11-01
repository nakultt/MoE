using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Controls;

/// <summary>
/// Shows a runtime state as a colored pill with an icon and text, so state is never conveyed by color alone.
/// </summary>
public sealed partial class StatusBadge : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Status"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(RuntimeStatus),
        typeof(StatusBadge),
        new PropertyMetadata(RuntimeStatus.Unloaded, OnStatusChanged));

    /// <summary>
    /// Identifies the <see cref="Text"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusBadge),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusBadge"/> class.
    /// </summary>
    public StatusBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisualState(false);
    }

    /// <summary>
    /// Gets or sets the runtime state shown by the badge.
    /// </summary>
    public RuntimeStatus Status
    {
        get => (RuntimeStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// Gets or sets the localized state text.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnStatusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((StatusBadge)sender).UpdateVisualState(true);
    }

    private void UpdateVisualState(bool useTransitions)
    {
        VisualStateManager.GoToState(this, $"Status{Status}", useTransitions);
    }
}
