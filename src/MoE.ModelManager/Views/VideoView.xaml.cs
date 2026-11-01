using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.Models;
using MoE.ModelManager.ViewModels;
using Windows.Media.Core;

namespace MoE.ModelManager.Views;

/// <summary>
/// Video generation workspace.
/// </summary>
public sealed partial class VideoView : UserControl
{
    public VideoView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        // The view stays alive while hidden, so limit Ctrl+Enter to focus inside this view.
        GenerateVideoAccelerator.ScopeOwner = VideoRoot;
        ViewModel.VideoGenerated += OnVideoGenerated;
        Unloaded += (_, _) => ViewModel.VideoGenerated -= OnVideoGenerated;
    }

    public MainViewModel ViewModel { get; }

    private void OnVideoGenerated(GeneratedVideoResult result)
    {
        GeneratedVideo.Source = MediaSource.CreateFromUri(new Uri(result.OutputPath));
        GeneratedVideo.Visibility = Visibility.Visible;
        VideoPlaceholder.Visibility = Visibility.Collapsed;
    }

    private async void OnOpenVideoClicked(object sender, RoutedEventArgs e)
    {
        await ShellActions.OpenFileAsync(ViewModel.GeneratedVideoPath);
    }

    private void OnShowVideoInFolderClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.ShowInFolder(ViewModel.GeneratedVideoPath);
    }

    private void OnCopyVideoPathClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.CopyText(ViewModel.GeneratedVideoPath);
    }
}
