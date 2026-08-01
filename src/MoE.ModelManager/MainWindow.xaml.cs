using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MoE.ModelManager.Services;
using Windows.Graphics;

namespace MoE.ModelManager;

/// <summary>
/// Hosts the model manager page and coordinates application shutdown.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = App.Services.GetRequiredService<IAppResources>().GetString("WindowTitle/Title");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1560, 920));
        Closed += OnClosed;
        RootFrame.Navigate(typeof(MainPage));
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        await App.ShutdownAsync();
    }
}
