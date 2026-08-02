using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MoE.ModelManager.Models;
using MoE.ModelManager.ViewModels;
using Windows.Media.Core;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace MoE.ModelManager;

/// <summary>
/// Hosts model controls, chat, and live request telemetry.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ViewModel.Messages.CollectionChanged += OnMessagesChanged;
        ViewModel.ImageGenerated += OnImageGenerated;
        ViewModel.SpeechGenerated += OnSpeechGenerated;
        ViewModel.VideoGenerated += OnVideoGenerated;
        ViewModel.ChatImageAttachmentCleared += OnChatImageAttachmentCleared;
    }

    public MainViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            ViewModel.RuntimeDetail = exception.Message;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Messages.CollectionChanged -= OnMessagesChanged;
        ViewModel.ImageGenerated -= OnImageGenerated;
        ViewModel.SpeechGenerated -= OnSpeechGenerated;
        ViewModel.VideoGenerated -= OnVideoGenerated;
        ViewModel.ChatImageAttachmentCleared -= OnChatImageAttachmentCleared;
        ViewModel.Dispose();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.Messages.Count > 0)
        {
            ChatList.ScrollIntoView(ViewModel.Messages[^1]);
        }
    }

    private async void OnImageGenerated(GeneratedImageResult result)
    {
        using InMemoryRandomAccessStream stream = new();
        using DataWriter writer = new(stream);
        writer.WriteBytes(result.Data);
        await writer.StoreAsync();
        stream.Seek(0);
        BitmapImage bitmap = new();
        bitmap.SetSource(stream);
        GeneratedImage.Source = bitmap;
        ImagePlaceholder.Visibility = Visibility.Collapsed;
    }

    private void OnVideoGenerated(GeneratedVideoResult result)
    {
        GeneratedVideo.Source = MediaSource.CreateFromUri(new Uri(result.OutputPath));
        GeneratedVideo.Visibility = Visibility.Visible;
        VideoPlaceholder.Visibility = Visibility.Collapsed;
    }

    private async void OnSelectAudioFileClicked(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".wav");
        Window? window = ((App)Application.Current).MainWindow;
        if (window is null)
        {
            return;
        }

        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.SelectedAudioFilePath = file.Path;
        }
    }

    private async void OnSelectChatImageClicked(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" })
        {
            picker.FileTypeFilter.Add(extension);
        }

        Window? window = ((App)Application.Current).MainWindow;
        if (window is null)
        {
            return;
        }

        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        ViewModel.AttachChatImage(file.Path);
        using IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        BitmapImage bitmap = new();
        await bitmap.SetSourceAsync(stream);
        ChatImagePreview.Source = bitmap;
        ChatImageAttachmentPanel.Visibility = Visibility.Visible;
    }

    private void OnRemoveChatImageClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearChatImageAttachment();
    }

    private void OnChatImageAttachmentCleared()
    {
        ChatImagePreview.Source = null;
        ChatImageAttachmentPanel.Visibility = Visibility.Collapsed;
    }

    private void OnSpeechGenerated(GeneratedSpeechResult result)
    {
        GeneratedSpeech.Source = MediaSource.CreateFromUri(new Uri(result.OutputPath));
        GeneratedSpeech.Visibility = Visibility.Visible;
    }
}
