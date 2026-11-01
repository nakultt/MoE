using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Helpers;

namespace MoE.ModelManager.Controls;

/// <summary>
/// Shows a labeled file or folder path with copy and reveal actions.
/// </summary>
public sealed partial class PathRow : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Label"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(PathRow), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="PathText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PathTextProperty = DependencyProperty.Register(
        nameof(PathText), typeof(string), typeof(PathRow), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Initializes a new instance of the <see cref="PathRow"/> class.
    /// </summary>
    public PathRow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the localized label.
    /// </summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Gets or sets the full path.
    /// </summary>
    public string PathText
    {
        get => (string)GetValue(PathTextProperty);
        set => SetValue(PathTextProperty, value);
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.CopyText(PathText);
    }

    private void OnShowInFolderClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.ShowInFolder(PathText);
    }
}
