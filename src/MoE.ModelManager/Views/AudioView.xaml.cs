using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Helpers;
using MoE.ModelManager.Models;
using MoE.ModelManager.ViewModels;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MoE.ModelManager.Views;

/// <summary>
/// Speech-to-text and text-to-speech workspace.
/// </summary>
public sealed partial class AudioView : UserControl
{
    public AudioView()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        ViewModel.SpeechGenerated += OnSpeechGenerated;
        Unloaded += (_, _) => ViewModel.SpeechGenerated -= OnSpeechGenerated;
    }

    public MainViewModel ViewModel { get; }

    private async void OnSelectAudioFileClicked(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".wav");
        if (!ShellActions.InitializeWithMainWindow(picker))
        {
            return;
        }

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.SelectedAudioFilePath = file.Path;
        }
    }

    private void OnSpeechGenerated(GeneratedSpeechResult result)
    {
        GeneratedSpeech.Source = MediaSource.CreateFromUri(new Uri(result.OutputPath));
        GeneratedSpeech.Visibility = Visibility.Visible;
    }

    private void OnShowSpeechInFolderClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.ShowInFolder(ViewModel.GeneratedSpeechPath);
    }

    private void OnCopySpeechPathClicked(object sender, RoutedEventArgs e)
    {
        ShellActions.CopyText(ViewModel.GeneratedSpeechPath);
    }
}
