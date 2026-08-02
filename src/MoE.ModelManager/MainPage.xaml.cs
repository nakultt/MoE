using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MoE.ModelManager.Models;
using MoE.ModelManager.ViewModels;
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
}
