using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Controls;

/// <summary>
/// Compact model identity, status, and Load/Eject actions shown in a workspace header.
/// </summary>
public sealed partial class RuntimeControlBar : UserControl
{
    /// <summary>
    /// Identifies the <see cref="LogoUri"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LogoUriProperty = DependencyProperty.Register(
        nameof(LogoUri), typeof(Uri), typeof(RuntimeControlBar), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="ModelName"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ModelNameProperty = DependencyProperty.Register(
        nameof(ModelName), typeof(string), typeof(RuntimeControlBar), new PropertyMetadata(string.Empty, OnModelNameChanged));

    /// <summary>
    /// Identifies the <see cref="Status"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(RuntimeStatus), typeof(RuntimeControlBar), new PropertyMetadata(RuntimeStatus.Unloaded));

    /// <summary>
    /// Identifies the <see cref="StatusText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(RuntimeControlBar), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="LoadCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LoadCommandProperty = DependencyProperty.Register(
        nameof(LoadCommand), typeof(ICommand), typeof(RuntimeControlBar), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="EjectCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty EjectCommandProperty = DependencyProperty.Register(
        nameof(EjectCommand), typeof(ICommand), typeof(RuntimeControlBar), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="LoadAutomationId"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LoadAutomationIdProperty = DependencyProperty.Register(
        nameof(LoadAutomationId), typeof(string), typeof(RuntimeControlBar), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="EjectAutomationId"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty EjectAutomationIdProperty = DependencyProperty.Register(
        nameof(EjectAutomationId), typeof(string), typeof(RuntimeControlBar), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeControlBar"/> class.
    /// </summary>
    public RuntimeControlBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the publisher logo.
    /// </summary>
    public Uri? LogoUri
    {
        get => (Uri?)GetValue(LogoUriProperty);
        set => SetValue(LogoUriProperty, value);
    }

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string ModelName
    {
        get => (string)GetValue(ModelNameProperty);
        set => SetValue(ModelNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the runtime state.
    /// </summary>
    public RuntimeStatus Status
    {
        get => (RuntimeStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// Gets or sets the localized runtime state text.
    /// </summary>
    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the command that loads the model.
    /// </summary>
    public ICommand? LoadCommand
    {
        get => (ICommand?)GetValue(LoadCommandProperty);
        set => SetValue(LoadCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command that ejects the model.
    /// </summary>
    public ICommand? EjectCommand
    {
        get => (ICommand?)GetValue(EjectCommandProperty);
        set => SetValue(EjectCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the UI automation identifier of the Load button.
    /// </summary>
    public string LoadAutomationId
    {
        get => (string)GetValue(LoadAutomationIdProperty);
        set => SetValue(LoadAutomationIdProperty, value);
    }

    /// <summary>
    /// Gets or sets the UI automation identifier of the Eject button.
    /// </summary>
    public string EjectAutomationId
    {
        get => (string)GetValue(EjectAutomationIdProperty);
        set => SetValue(EjectAutomationIdProperty, value);
    }

    private static void OnModelNameChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        RuntimeControlBar bar = (RuntimeControlBar)sender;
        IAppResources resources = App.Services.GetRequiredService<IAppResources>();
        string name = args.NewValue as string ?? string.Empty;
        AutomationProperties.SetName(
            bar.LoadButton,
            string.Format(CultureInfo.CurrentCulture, resources.GetString("LoadModelAutomationFormat"), name));
        AutomationProperties.SetName(
            bar.EjectButton,
            string.Format(CultureInfo.CurrentCulture, resources.GetString("EjectModelAutomationFormat"), name));
    }
}
