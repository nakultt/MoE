using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.Models;
using MoE.ModelManager.ViewModels;
using Windows.Storage.Streams;

namespace MoE.ModelManager.Views;

/// <summary>
/// Image generation workspace.
/// </summary>
public sealed partial class ImagesView : UserControl
{
    public ImagesView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        ViewModel.ImageGenerated += OnImageGenerated;
        Unloaded += (_, _) => ViewModel.ImageGenerated -= OnImageGenerated;
    }

    public MainViewModel ViewModel { get; }

    private async void OnImageGenerated(GeneratedImageResult result)
    {
        using InMemoryRandomAccessStream stream = new();
        using DataWriter writer = new(stream);
        writer.WriteBytes(result.Data);
        await writer.StoreAsync();
        stream.Seek(0);
        BitmapImage bitmap = new();
        await bitmap.SetSourceAsync(stream);
        GeneratedImage.Source = bitmap;
        ImagePlaceholder.Visibility = Visibility.Collapsed;
    }

    private async void OnOpenImageClicked(object sender, RoutedEventArgs e)
    {
        await ShellActions.OpenFileAsync(ViewModel.GeneratedImagePath);
    }

    private void OnShowImageInFolderClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.ShowInFolder(ViewModel.GeneratedImagePath);
    }

    private void OnCopyImagePathClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.CopyText(ViewModel.GeneratedImagePath);
    }
}
