using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;
using MoE.ModelManager.ViewModels;
using Windows.Graphics;

namespace MoE.ModelManager;

/// <summary>
/// Hosts the navigation shell, applies the app theme, and coordinates application shutdown.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = App.Services.GetRequiredService<IAppResources>().GetString("WindowTitle/Title");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1560, 920));
        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        _viewModel.ThemeChanged += ApplyTheme;
        RootGrid.ActualThemeChanged += OnActualThemeChanged;
        ApplyTheme(App.Services.GetRequiredService<IAppSettingsService>().Current.Theme);
        Closed += OnClosed;
        RootFrame.Navigate(typeof(MainPage));
    }

    private void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            AppSettings.LightTheme => ElementTheme.Light,
            AppSettings.DarkTheme => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        UpdateCaptionButtonColors();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateCaptionButtonColors();
    }

    private void UpdateCaptionButtonColors()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        AppWindowTitleBar titleBar = AppWindow.TitleBar;
        bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;
        Windows.UI.Color foreground = isDark ? Colors.White : Colors.Black;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = isDark
            ? ColorHelper.FromArgb(0x80, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x80, 0x00, 0x00, 0x00);
        titleBar.ButtonHoverBackgroundColor = isDark
            ? ColorHelper.FromArgb(0x15, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x0F, 0x00, 0x00, 0x00);
        titleBar.ButtonPressedBackgroundColor = isDark
            ? ColorHelper.FromArgb(0x0B, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x0A, 0x00, 0x00, 0x00);
    }

    private void OnPaneToggleRequested(TitleBar sender, object args)
    {
        if (RootFrame.Content is MainPage page)
        {
            page.TogglePane();
        }
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        _viewModel.ThemeChanged -= ApplyTheme;
        await App.ShutdownAsync();
    }
}
