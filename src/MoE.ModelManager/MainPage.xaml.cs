using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.ViewModels;

namespace MoE.ModelManager;

/// <summary>
/// The navigation shell that hosts every workspace and keeps them alive while switching.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        NavView.SelectedItem = ChatNavItem;
        Loaded += OnLoaded;
    }

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Opens or closes the navigation pane from the title bar button.
    /// </summary>
    internal void TogglePane()
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private static Visibility ToVisibility(bool isVisible)
    {
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.InitializeAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or InvalidDataException)
        {
            ViewModel.RuntimeDetail = exception.Message;
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string section = args.IsSettingsSelected
            ? "Settings"
            : args.SelectedItemContainer?.Tag as string ?? "Chat";
        ChatSection.Visibility = ToVisibility(section == "Chat");
        ImagesSection.Visibility = ToVisibility(section == "Images");
        VideoSection.Visibility = ToVisibility(section == "Video");
        AudioSection.Visibility = ToVisibility(section == "Audio");
        ModelsSection.Visibility = ToVisibility(section == "Models");
        ApiSection.Visibility = ToVisibility(section == "Api");
        SettingsSection.Visibility = ToVisibility(section == "Settings");
    }
}
