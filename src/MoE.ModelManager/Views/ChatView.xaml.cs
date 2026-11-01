using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;
using MoE.ModelManager.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace MoE.ModelManager.Views;

/// <summary>
/// Chat workspace with saved conversations, streaming replies, and media attachments.
/// </summary>
public sealed partial class ChatView : UserControl
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];
    private static readonly string[] AudioExtensions = [".wav", ".mp3", ".flac"];
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".webm", ".mov", ".avi"];

    private static readonly TimeSpan HistoryPaneDuration = TimeSpan.FromMilliseconds(250);

    private readonly IAppResources _resources;
    private readonly double _historyPaneWidth;
    private Storyboard? _historyPaneStoryboard;

    public ChatView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        _resources = App.Services.GetRequiredService<IAppResources>();
        InitializeComponent();

        _historyPaneWidth = HistoryPane.Width;
        SetHistoryPaneOpen(ViewModel.IsHistoryPaneOpen, animate: false);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.ChatAttachmentCleared += OnChatAttachmentCleared;
        Unloaded += OnUnloaded;
    }

    public MainViewModel ViewModel { get; }

    private static T? GetItem<T>(object sender)
        where T : class
    {
        return sender is FrameworkElement element
            ? element.Tag as T ?? element.DataContext as T
            : null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.ChatAttachmentCleared -= OnChatAttachmentCleared;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsHistoryPaneOpen))
        {
            SetHistoryPaneOpen(ViewModel.IsHistoryPaneOpen, animate: true);
        }
    }

    // Slides the History pane by animating its host's width (the pane itself keeps a fixed
    // width and is clipped), and fades it, so the conversation column resizes smoothly.
    private void SetHistoryPaneOpen(bool isOpen, bool animate)
    {
        double fromWidth = HistoryPaneHost.Visibility == Visibility.Visible ? HistoryPaneHost.ActualWidth : 0;
        double fromOpacity = HistoryPane.Opacity;
        _historyPaneStoryboard?.Stop();
        _historyPaneStoryboard = null;

        double toWidth = isOpen ? _historyPaneWidth : 0;
        double toOpacity = isOpen ? 1 : 0;
        if (!animate || !new UISettings().AnimationsEnabled)
        {
            HistoryPaneHost.Width = toWidth;
            HistoryPane.Opacity = toOpacity;
            HistoryPaneHost.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        HistoryPaneHost.Visibility = Visibility.Visible;
        HistoryPaneHost.Width = fromWidth;
        HistoryPane.Opacity = fromOpacity;
        EasingFunctionBase easing = new CubicEase { EasingMode = isOpen ? EasingMode.EaseOut : EasingMode.EaseIn };
        DoubleAnimation widthAnimation = new()
        {
            To = toWidth,
            Duration = HistoryPaneDuration,
            EasingFunction = easing,
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(widthAnimation, HistoryPaneHost);
        Storyboard.SetTargetProperty(widthAnimation, nameof(Width));
        DoubleAnimation opacityAnimation = new()
        {
            To = toOpacity,
            Duration = HistoryPaneDuration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(opacityAnimation, HistoryPane);
        Storyboard.SetTargetProperty(opacityAnimation, nameof(Opacity));

        Storyboard storyboard = new();
        storyboard.Children.Add(widthAnimation);
        storyboard.Children.Add(opacityAnimation);
        storyboard.Completed += (_, _) =>
        {
            if (_historyPaneStoryboard != storyboard)
            {
                return;
            }

            // Hold the final values as local values so a later Stop() does not snap back.
            _historyPaneStoryboard = null;
            storyboard.Stop();
            HistoryPaneHost.Width = toWidth;
            HistoryPane.Opacity = toOpacity;
            HistoryPaneHost.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        };
        _historyPaneStoryboard = storyboard;
        storyboard.Begin();
    }

    // Enter sends; Shift+Enter falls through so the TextBox inserts a new line.
    private void OnPromptPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter
            || InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
        {
            return;
        }

        e.Handled = true;
        if (ViewModel.SendMessageCommand.CanExecute(null))
        {
            ViewModel.SendMessageCommand.Execute(null);
        }
    }

    private async void OnSelectChatMediaClicked(object sender, RoutedEventArgs e)
    {
        ModelDefinition? model = ViewModel.SelectedModel;
        if (model is null)
        {
            return;
        }

        FileOpenPicker picker = new();
        List<string> extensions = [];
        if (model.HasVision)
        {
            extensions.AddRange(ImageExtensions);
        }

        if (model.SupportsAudio)
        {
            extensions.AddRange(AudioExtensions);
        }

        if (model.SupportsVideo)
        {
            extensions.AddRange(VideoExtensions);
        }

        foreach (string extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        if (!ShellActions.InitializeWithMainWindow(picker))
        {
            return;
        }

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            ViewModel.AttachChatMedia(file.Path);
        }
        catch (InvalidOperationException exception)
        {
            await ShowMessageAsync(_resources.GetString("ErrorChatMediaFailed"), exception.Message);
            return;
        }

        string selectedExtension = Path.GetExtension(file.Path).ToLowerInvariant();
        if (ImageExtensions.Contains(selectedExtension))
        {
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapImage bitmap = new() { DecodePixelHeight = 88 };
            await bitmap.SetSourceAsync(stream);
            ChatImagePreview.Source = bitmap;
            ChatImagePreview.Visibility = Visibility.Visible;
        }
        else
        {
            ChatImagePreview.Source = null;
            ChatImagePreview.Visibility = Visibility.Collapsed;
        }

        ChatImageAttachmentPanel.Visibility = Visibility.Visible;
    }

    private void OnRemoveChatMediaClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearChatAttachment();
    }

    private void OnChatAttachmentCleared()
    {
        ChatImagePreview.Source = null;
        ChatImagePreview.Visibility = Visibility.Visible;
        ChatImageAttachmentPanel.Visibility = Visibility.Collapsed;
    }

    private void OnCopyMessageClicked(object sender, RoutedEventArgs e)
    {
        if (GetItem<ChatMessage>(sender) is { } message)
        {
            ShellActions.CopyText(message.Content);
        }
    }

    private async void OnRegenerateMessageClicked(object sender, RoutedEventArgs e)
    {
        if (GetItem<ChatMessage>(sender) is { } message)
        {
            await ViewModel.RegenerateAsync(message);
        }
    }

    private async void OnHistoryItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChatConversationSummary summary)
        {
            await ViewModel.OpenConversationCommand.ExecuteAsync(summary);
        }
    }

    private void OnTurnOnHistoryClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsChatHistoryEnabled = true;
    }

    private async void OnRenameConversationClicked(object sender, RoutedEventArgs e)
    {
        if (GetItem<ChatConversationSummary>(sender) is not { } summary)
        {
            return;
        }

        TextBox titleBox = new()
        {
            Text = summary.Title,
            MaxLength = 120,
        };
        titleBox.SelectAll();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(titleBox, _resources.GetString("RenameChatDialogTitle"));
        ContentDialog dialog = ShellActions.CreateDialog(this);
        dialog.Title = _resources.GetString("RenameChatDialogTitle");
        dialog.Content = titleBox;
        dialog.PrimaryButtonText = _resources.GetString("RenameChatDialogPrimary");
        dialog.CloseButtonText = _resources.GetString("DialogCancel");
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RenameConversationAsync(summary, titleBox.Text);
        }
    }

    private async void OnDeleteConversationClicked(object sender, RoutedEventArgs e)
    {
        if (GetItem<ChatConversationSummary>(sender) is not { } summary)
        {
            return;
        }

        ContentDialog dialog = ShellActions.CreateDialog(this);
        dialog.Title = _resources.GetString("DeleteChatDialogTitle");
        dialog.Content = new TextBlock
        {
            Text = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _resources.GetString("DeleteChatDialogMessageFormat"),
                summary.Title),
            TextWrapping = TextWrapping.Wrap,
        };
        dialog.PrimaryButtonText = _resources.GetString("DeleteChatDialogPrimary");
        dialog.CloseButtonText = _resources.GetString("DialogCancel");
        dialog.DefaultButton = ContentDialogButton.Close;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteConversationCommand.ExecuteAsync(summary);
        }
    }

    private async void OnExportConversationClicked(object sender, RoutedEventArgs e)
    {
        if (GetItem<ChatConversationSummary>(sender) is not { } summary)
        {
            return;
        }

        string? markdown = await ViewModel.ExportConversationMarkdownAsync(summary);
        if (markdown is not null)
        {
            await SaveMarkdownAsync(markdown, ChatTranscriptFormatter.CreateFileName(summary.Title));
        }
    }

    private async void OnExportCurrentChatClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasNoMessages)
        {
            return;
        }

        (string markdown, string fileName) = ViewModel.ExportCurrentChatMarkdown();
        await SaveMarkdownAsync(markdown, fileName);
    }

    private async Task SaveMarkdownAsync(string markdown, string fileName)
    {
        FileSavePicker picker = new()
        {
            SuggestedFileName = fileName,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeChoices.Add(_resources.GetString("MarkdownFileType"), [".md"]);
        if (!ShellActions.InitializeWithMainWindow(picker))
        {
            return;
        }

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await FileIO.WriteTextAsync(file, markdown);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync(_resources.GetString("ErrorExportFailed"), exception.Message);
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        ContentDialog dialog = ShellActions.CreateDialog(this);
        dialog.Title = title;
        dialog.Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        dialog.CloseButtonText = _resources.GetString("DialogOk");
        dialog.DefaultButton = ContentDialogButton.Close;
        await dialog.ShowAsync();
    }
}
