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
/// A settings-style card that shows one model runtime with its publisher, status, actions, and files.
/// </summary>
public sealed partial class ModelCard : UserControl
{
    /// <summary>
    /// Identifies the <see cref="LogoUri"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LogoUriProperty = DependencyProperty.Register(
        nameof(LogoUri), typeof(Uri), typeof(ModelCard), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="Kind"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="Publisher"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PublisherProperty = DependencyProperty.Register(
        nameof(Publisher), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="ModelName"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ModelNameProperty = DependencyProperty.Register(
        nameof(ModelName), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty, OnModelNameChanged));

    /// <summary>
    /// Identifies the <see cref="Description"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="Tags"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(
        nameof(Tags), typeof(object), typeof(ModelCard), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="Status"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(RuntimeStatus), typeof(ModelCard), new PropertyMetadata(RuntimeStatus.Unloaded));

    /// <summary>
    /// Identifies the <see cref="StatusText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="ErrorText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(
        nameof(ErrorText), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="LoadCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LoadCommandProperty = DependencyProperty.Register(
        nameof(LoadCommand), typeof(ICommand), typeof(ModelCard), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="EjectCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty EjectCommandProperty = DependencyProperty.Register(
        nameof(EjectCommand), typeof(ICommand), typeof(ModelCard), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="LoadAutomationId"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LoadAutomationIdProperty = DependencyProperty.Register(
        nameof(LoadAutomationId), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="EjectAutomationId"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty EjectAutomationIdProperty = DependencyProperty.Register(
        nameof(EjectAutomationId), typeof(string), typeof(ModelCard), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="Accessory"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AccessoryProperty = DependencyProperty.Register(
        nameof(Accessory), typeof(object), typeof(ModelCard), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="Details"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DetailsProperty = DependencyProperty.Register(
        nameof(Details), typeof(object), typeof(ModelCard), new PropertyMetadata(null));

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCard"/> class.
    /// </summary>
    public ModelCard()
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
    /// Gets or sets the localized model category, such as "Language model".
    /// </summary>
    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Gets or sets the model publisher.
    /// </summary>
    public string Publisher
    {
        get => (string)GetValue(PublisherProperty);
        set => SetValue(PublisherProperty, value);
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
    /// Gets or sets the model description.
    /// </summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets optional capability tags, as a list of strings.
    /// </summary>
    public object? Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
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
    /// Gets or sets the last load error, shown in an error bar.
    /// </summary>
    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
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

    /// <summary>
    /// Gets or sets optional content shown before the status, such as a model picker.
    /// </summary>
    public object? Accessory
    {
        get => GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    /// <summary>
    /// Gets or sets optional content shown in the expandable files section.
    /// </summary>
    public object? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    private static void OnModelNameChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ModelCard card = (ModelCard)sender;
        IAppResources resources = App.Services.GetRequiredService<IAppResources>();
        string name = args.NewValue as string ?? string.Empty;
        AutomationProperties.SetName(
            card.LoadButton,
            string.Format(CultureInfo.CurrentCulture, resources.GetString("LoadModelAutomationFormat"), name));
        AutomationProperties.SetName(
            card.EjectButton,
            string.Format(CultureInfo.CurrentCulture, resources.GetString("EjectModelAutomationFormat"), name));
    }
}
