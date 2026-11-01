using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;
using MoE.ModelManager.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace MoE.ModelManager.Views;

/// <summary>
/// Chat workspace with saved conversations, streaming replies, and media attachments.
/// </summary>
public sealed partial class ChatView : UserControl
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];
    private static readonly string[] AudioExtensions = [".wav", ".mp3", ".flac"];
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".webm", ".mov", ".avi"];

    private readonly IAppResources _resources;

    public ChatView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        _resources = App.Services.GetRequiredService<IAppResources>();
        InitializeComponent();

        ViewModel.Messages.CollectionChanged += OnMessagesChanged;
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
        ViewModel.Messages.CollectionChanged -= OnMessagesChanged;
        ViewModel.ChatAttachmentCleared -= OnChatAttachmentCleared;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.Messages.Count > 0)
        {
            ChatList.ScrollIntoView(ViewModel.Messages[^1]);
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
